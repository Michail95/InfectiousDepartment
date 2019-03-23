using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace InfectiousDepartment
{
    
    public partial class InfectiousDepartmentForm : Form
    {
        
        delegate void ProtocolDelegate(String str);
        delegate void PatientDelegate(Patient patient);
        delegate void ThreadDelegate(Thread thread);


        
        public class Doctor
        {
            /// <summary>
            /// Поля класса Doctor
            /// </summary>
            private InfectiousDepartmentForm _form;
            private Patient _patient = null;                                    //  Пациент, принимаемый врачом
            private TimeSpan _duration = new TimeSpan(0);                       //  Время работы врача
            private int _number = 0;                                            //  Номер врача
            private int _patients = 0;                                          //  количество принятых пациентов


            
            public int Number
            {
                get
                {
                    return _number;
                }
            }


            //Возвращает и устанавливает ссылку на объект пациента, принимаемом данным врачом
            public Patient Patient
            {
                get
                {
                    return _patient;
                }

                set
                {
                    _patient = value;
                }
            }


            
            public TimeSpan Duration
            {
                get
                {
                    return _duration;
                }
            }


            //Возвращает количество принятых пациентов
            public int Patients
            {
                get
                {
                    return _patients;
                }
            }


            
            public Doctor(InfectiousDepartmentForm form)
            {
                
                _form = form;
                _number = ++form._doctorNumber;
            }


            //Определяет время работы врача
            public void Working(TimeSpan duration)
            {
                if (_patients == 0)    
                {
                    _duration = duration;
                }
                else
                {
                    _duration = _duration + duration;
                }
                _patients++;
            }
        }


        
        public class Patient
        {
            /// <summary>
            /// Поля класса Patient
            /// </summary>
            private InfectiousDepartmentForm _form = null;
            private Thread _thread = null;
            private Semaphore _restart = new Semaphore(1, 1);
            private Doctor _doctor = null;
            private Doctor _consultant = null;
            private int _number = 0;                                                //  Номер пациента
            private bool _infected = false;                                         //  Признак, что пациент заражен
            private bool _inRegistrationQueue = true;                               //  Признак нахождения в очереди регистрации


           
            public int Number
            {
                get
                {
                    return _number;
                }
            }


            
            public bool IsInfected
            {
                get
                {
                    return _infected;
                }

                set
                {
                    _infected = value;
                }
            }


            
            public bool InRegistationQueue
            {
                get
                {
                    return _inRegistrationQueue;
                }

                set
                {
                    _inRegistrationQueue = value;
                }
            }


            
            public Semaphore Semaphore
            {
                get
                {
                    return _restart;
                }
            }


            /// <summary>
            /// Свойство Thread класса Patient
            /// Получает и устанавливает ссылку на поток, работающий в текущем объекте
            /// </summary>
            public Thread Thread
            {
                get
                {
                    return _thread;
                }

                set
                {
                    _thread = value;
                }
            }


           
            public Patient(InfectiousDepartmentForm form, bool infected)
            {
                _form = form;
                _infected = infected;
                _number = ++form._patientNumber;
            }


            public void Run()
            {
                Random random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);
                DateTime doctorTime1 = DateTime.Now;
                DateTime doctorTime2 = DateTime.Now;
                DateTime consultantTime1 = DateTime.Now;
                DateTime consultantTime2 = DateTime.Now;
                String str;

                
                TimerCallback callback = new TimerCallback(SetInfected);
                System.Threading.Timer timer = new System.Threading.Timer(callback, this, _form._infectionTime * 1000, Timeout.Infinite);

                Thread.Sleep(1000);

                //  Ждем перевода в очередь смотровой комнаты
                _restart.WaitOne();
                
                
                timer.Dispose();

                //  Получаем время приема пациента врачом и признак необходимости консультации
                int receiptTime = random.Next(1, _form._maxReceiptTime);
                bool isConsultation = (receiptTime % 3 == 0) ? true : false;

                //  Пациент направляется к врачу
                _form._doctorsAvailable.WaitOne();
                lock (_form._freeDoctors)
                {
                    _doctor = _form._freeDoctors.Dequeue();
                    _doctor.Patient = this;
                }
                doctorTime1 = DateTime.Now;
                str = "---> Врач " + _doctor.Number.ToString() +  " начал прием пациента " + _number.ToString() + " в " +
                    doctorTime1.ToString() + "\n";
                _form.Invoke(_form.ToProtocolDelegate, new Object[] { str });

                //  При необходимости консультации
                if (isConsultation)
                {
                    str = "---> Врачу " + _doctor.Number.ToString() + " при приеме пациента " + _number.ToString() + 
                        " потребовалась консультация в " + DateTime.Now.ToString() + "\n";
                    _form.Invoke(_form.ToProtocolDelegate, new Object[] { str });

                    //  Выбираем консультанта, ждем некоторое время
                    if (_form._doctorsAvailable.WaitOne(_form._maxReceiptTime * 1000))
                    {
                        //  Есть доступный консультант
                        lock (_form._freeDoctors)
                        {
                            _consultant = _form._freeDoctors.Dequeue();
                            _consultant.Patient = this;
                        }
                        consultantTime1 = DateTime.Now;
                        str = "<--> Консультант " + _consultant.Number.ToString() + " присоединился к врачу " + _doctor.Number.ToString() +
                            " при приеме пациента " + _number.ToString() + " в " + consultantTime1.ToString() + "\n";
                        _form.Invoke(_form.ToProtocolDelegate, new Object[] { str });
                    }
                    else
                    {
                        //  Врач сам разобрался с диагнозом
                        str = "---> Врач " + _doctor.Number.ToString() + " сам определился с диагнозом пациента " + _number.ToString() +
                            " в " + DateTime.Now.ToString() + "\n";
                        _form.Invoke(_form.ToProtocolDelegate, new Object[] { str });
                    }
                }

                //  Прием у врача
                Thread.Sleep(receiptTime * 1000);

                //  Если был приглашен консультант, то он завершил прием пациента
                if (isConsultation && _consultant != null)
                {
                    consultantTime2 = DateTime.Now;
                    _consultant.Working(consultantTime2 - consultantTime1);
                    str = "<--> Консультант " + _consultant.Number.ToString() + " завершил прием пациента " + _number.ToString() + 
                        " в " + consultantTime2.ToString() + "\n";
                    _form.Invoke(_form.ToProtocolDelegate, new Object[] { str });

                    lock (_form._freeDoctors)
                    {
                        _consultant.Patient = null;
                        _form._freeDoctors.Enqueue(_consultant);
                        _consultant = null;
                    }
                    _form._doctorsAvailable.Release();
                }

                //  Врач завершил прием пациента
                doctorTime2 = DateTime.Now;
                _doctor.Working(doctorTime2 - doctorTime1);
                str = "---> Врач " + _doctor.Number.ToString() + " завершил прием пациента " + _number.ToString() +
                    " в " + doctorTime2.ToString() + "\n";
                _form.Invoke(_form.ToProtocolDelegate, new Object[] { str });

                lock (_form._freeDoctors)
                {
                    _doctor.Patient = null;
                    _form._freeDoctors.Enqueue(_doctor);
                    _doctor = null;
                }
                _form._doctorsAvailable.Release();

                //  Пациент покидает смотровую комнату
                str = "---- Пациент " + _number.ToString() + " покинул смотровую комнату в " + DateTime.Now.ToString() + "\n";
                _form.Invoke(_form.ToProtocolDelegate, new Object[] { str });

                //  Удаляем пациента из списка находящихся в смотровой комнате
                lock (_form._observationRoomQueue)
                {
                    _form._observationRoomQueue.Remove(this);
                }
              //  timer.Dispose();
                //  Сигнализация о завершении потока объекта пациента
                _form.Invoke(_form.RemoveCompletedThread, new Object[] { _thread });
            }


       
            public void SetInfected(Object state)
            {
                Patient patient = (Patient)state;

                //  Если пациент находится в очереди регистратуры, устанавливаем признак инфицирования
                lock (patient._form._registrationQueue)
                {
                    lock (patient)
                    {
                        if (patient._inRegistrationQueue)
                        {
                            if (!patient._infected && patient._form._infected > 0)
                            {
                                patient._infected = true;
                                patient._form._infected++;

                                String str = "!!!!Пациент " + patient.Number.ToString() + " был инфицирован в " + 
                                    DateTime.Now.ToString() + " !!!!\n";
                                _form.Invoke(patient._form.ToProtocolDelegate, new Object[] { str });
                            }
                        }
                    }
                }
            }
        }


        private List<Patient> _registrationQueue = new List<Patient>();
        private List<Patient> _observationRoomQueue = new List<Patient>();
        private Queue<Doctor> _freeDoctors = new Queue<Doctor>();
        private List<Thread> _threads = new List<Thread>();
        private Thread _registrationThread = null;
        private Thread _observationRoomThread = null;
        private Semaphore _doctorsAvailable = null;
        private int _doctors = 10;
        private int _observationRoomCapacity = 11;
        private int _maxReceiptTime = 5;
        private int _maxRegistrationTime = 5;
        private int _infectionTime = 20;
        private int _doctorNumber = 0;
        private int _patientNumber = 0;
        private int _infected = 0;
        private volatile bool _stop = false;


        
        private ProtocolDelegate ToProtocolDelegate;
        private PatientDelegate PatientEntryRegistration;
        private PatientDelegate PatientEntryObservationRoom;
        private ThreadDelegate RemoveCompletedThread;


        
        [STAThread]
        static void Main()
        {
            
            InfectiousDepartmentForm form = new InfectiousDepartmentForm();
            form.ShowDialog();
        }


        public InfectiousDepartmentForm()
        {
            //  Инициализация формы
            InitializeComponent();

            //  Устанавливаем начальные значения
            numericUpDownDoctors.Value = _doctors;
            numericUpDownObservationRoomCapacity.Value = _observationRoomCapacity;
            numericUpDownReceiptTime.Value = _maxReceiptTime;
            numericUpDownRegistrftionTime.Value = _maxRegistrationTime;
            numericUpDownInfectionTime.Value = _infectionTime;

            
            ToProtocolDelegate = new ProtocolDelegate
            (
                delegate (String str)
                {
                    richTextBoxProtocol.AppendText(str);
                }
            );

            PatientEntryRegistration = new PatientDelegate
            (
                delegate (Patient patient)
                {
                    Semaphore semaphore = patient.Semaphore;
                    semaphore.WaitOne();

                    
                    Thread thread = new Thread(patient.Run);
                    _threads.Add(thread);
                    patient.Thread = thread;
                    thread.Start();
                }
            );

            PatientEntryObservationRoom = new PatientDelegate
            (
                delegate (Patient patient)
                {
                    patient.InRegistationQueue = false;
                    Semaphore semaphore = patient.Semaphore;
                    semaphore.Release();
                }
            );

            
            RemoveCompletedThread = new ThreadDelegate
            (
                delegate (Thread thread)
                {
                    _threads.Remove(thread);
                    if (_threads.Count == 0)
                    {
                        
                        _doctorsAvailable.Dispose();

                        foreach (Doctor doctor in _freeDoctors)
                        {
                            String str = String.Format("Врач {0} принял {1} пациентов за время {2}\n", doctor.Number, doctor.Patients,
                                doctor.Duration);
                            richTextBoxProtocol.AppendText(str);
                        }

                        
                        _freeDoctors.Clear();
                        _registrationThread = null;
                        _observationRoomThread = null;

                        
                        SwapEnables();
                 //////
                    }
                }
            );
        }


        private void buttonStartReceipt_Click(object sender, EventArgs e)
        {
            String str;

            
            richTextBoxProtocol.Clear();

            
            _registrationQueue.Clear();
            _observationRoomQueue.Clear();
            _freeDoctors.Clear();
            _threads.Clear();
            _registrationThread = null;
            _observationRoomThread = null;

           
            _doctors = (int)numericUpDownDoctors.Value;
            _observationRoomCapacity = (int)numericUpDownObservationRoomCapacity.Value;
            _maxReceiptTime = (int)numericUpDownReceiptTime.Value;
            _maxRegistrationTime = (int)numericUpDownRegistrftionTime.Value;
            _infectionTime = (int)numericUpDownInfectionTime.Value;

            
            str = "Количество врачей = " + _doctors.ToString() + "\nКоличество мест в смотровой комнате = " + 
                _observationRoomCapacity.ToString() + "\nМаксимальное время приема врачом = " + _maxReceiptTime +
                "\nМаксимальное время регистрации пациента = " + _maxRegistrationTime.ToString() + "\nВремя инфицирования = " +
                _infectionTime.ToString() + "\n";
            richTextBoxProtocol.AppendText(str);

            //  Инициализация параметров выполнения
            _doctorNumber = 0;
            _patientNumber = 0;
            _stop = false;

            
            SwapEnables();

            //  Формируем очередь докторов
            for (int i = 0; i < _doctors; i++)
            {
                Doctor doctor = new Doctor(this);
                _freeDoctors.Enqueue(doctor);
            }
            _doctorsAvailable = new Semaphore(_doctors, _doctors);

            //  Запуск потока регистратуры
            _registrationThread = new Thread(ThreadRegistration);
            _registrationThread.Start(this);
            _threads.Add(_registrationThread);

            //  Запуск потока смотровой комнаты
            _observationRoomThread = new Thread(ThreadObservationRoom);
            _observationRoomThread.Start(this);
            _threads.Add(_observationRoomThread);
        }


        
        private void buttonStopReceipt_Click(object sender, EventArgs e)
        {
            
            _stop = true;
        }


        
        private void numericUpDownDoctors_Leave(object sender, EventArgs e)
        {
            numericUpDownObservationRoomCapacity.Minimum = numericUpDownDoctors.Value;
        }


        
        private void SwapEnables()
        {
            numericUpDownDoctors.Enabled = !numericUpDownDoctors.Enabled;
            numericUpDownObservationRoomCapacity.Enabled = !numericUpDownObservationRoomCapacity.Enabled;
            numericUpDownReceiptTime.Enabled = !numericUpDownReceiptTime.Enabled;
            numericUpDownRegistrftionTime.Enabled = !numericUpDownRegistrftionTime.Enabled;
            numericUpDownInfectionTime.Enabled = !numericUpDownInfectionTime.Enabled;
            buttonStartReceipt.Enabled = !buttonStartReceipt.Enabled;
            buttonStopReceipt.Enabled = !buttonStopReceipt.Enabled;
            buttonExit.Enabled = !buttonExit.Enabled;
        }


        
        private void ThreadRegistration(Object obj)
        {
            InfectiousDepartmentForm form = (InfectiousDepartmentForm)obj;
            String str;

            //  Сигнализируем о начале работы регистратуры
            str = "Регистратура начала работу в " + DateTime.Now.ToString() + "\n";
            form.Invoke(form.ToProtocolDelegate, new Object[] { str });

            
            Random random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);

            //  Выполняем, пока не получена команда завершения
            while (!form._stop)
            {
                
                int delay = Convert.ToInt32(random.NextDouble() * 1000 * form._maxRegistrationTime);
                bool isInfected = (delay % 2 == 0) ? false : true;

               
                Patient patient = new Patient(this, isInfected);

                
                str = "---- Поступил " + ((isInfected) ? "инфицированный" : "здоровый") + " пациент " + patient.Number.ToString() + 
                    " в " + DateTime.Now.ToString() + "\n";
                form.Invoke(form.ToProtocolDelegate, new Object[] { str });

                //  И помещаем пациента в общую очередь
                lock (form._registrationQueue)
                {
                    form._registrationQueue.Add(patient);

                    //  Если пациент инфицирован, увеличим счетчик
                    if (patient.IsInfected)
                        form._infected++;
                }
                form.Invoke(PatientEntryRegistration, new Object[] { patient });

                //  Ждем следующего пациента
                Thread.Sleep(delay);
            }

            //  Сигнализируем о завершении работы регистратуры
            str = "Регистратура закончила работу в " + DateTime.Now.ToString() + "\n";
            form.Invoke(form.ToProtocolDelegate, new Object[] { str });

            //  Удаляем ссылку на поток из списка
            form.Invoke(form.RemoveCompletedThread, new Object[] { form._registrationThread });
        }


        
        private void ThreadObservationRoom(Object obj)
        {
            InfectiousDepartmentForm form = (InfectiousDepartmentForm)obj;
            Patient patient;
            String str;
            bool isInfected = false;
            bool isExit = false;

            //  Сигнализируем о начале работы смотровой комнаты
            str = "Смотровая комната начала работу в " + DateTime.Now.ToString() + "\n";
            form.Invoke(form.ToProtocolDelegate, new Object[] { str });

            //  Основной цикл потока
            while (!isExit)
            {
                lock (form._registrationQueue)
                {
                    lock (form._observationRoomQueue)
                    {
                        //  Если нужно завершить работу, завершим
                        if (form._stop && form._registrationQueue.Count == 0 && form._observationRoomQueue.Count == 0)
                        {
                            isExit = true;
                        }
                        else
                        {
                            //  Если очередь регистратуры не пуста
                            if (form._registrationQueue.Count != 0)
                            {
                                //  Помещаем пациентов в смотровую комнату
                                if (form._observationRoomQueue.Count == 0)
                                {
                                    //  Определяем, здоровых или инфицированных пациентов будем принимать по первому в очереди
                                    patient = form._registrationQueue[0];
                                    
                                    //  Блокируем доступ к объекту пациента
                                    lock (patient)
                                    {
                                        
                                        form._registrationQueue.Remove(patient);
                                        isInfected = patient.IsInfected;

                                        //  Если пациент инфицирован, уменьшим счетчик
                                        if (patient.IsInfected)
                                            form._infected--;

                                        //  Пациент перемещается в смотровую комнату
                                        form.Invoke(PatientEntryObservationRoom, new Object[] { patient });
                                        form._observationRoomQueue.Add(patient);

                                        
                                        str = "---- Пациент " + patient.Number.ToString() + " перешел в смотровую комнату в " +
                                            DateTime.Now.ToString() + "\n";
                                        form.Invoke(form.ToProtocolDelegate, new Object[] { str });
                                    }
                                }

                                //  Добавляем либо здоровых либо инфицированных пациентов, в зависимости от того,
                                //  какие уже есть в очереди
                                int i = 0;
                                while (form._observationRoomQueue.Count < form._observationRoomCapacity)
                                {
                                    //  Если в очереди регистратуры нет таких пациентов, выйдем из цикла
                                    if (i == form._registrationQueue.Count)
                                    {
                                        break;
                                    }

                                    //  Смотрим, что за пациент в очереди
                                    patient = form._registrationQueue[i];

                                    
                                    lock (patient)
                                    {
                                        if (patient.IsInfected == isInfected)
                                        {
                                            
                                            form._registrationQueue.Remove(patient);

                                            //  Если пациент инфицирован, уменьшим счетчик
                                            if (patient.IsInfected)
                                                form._infected--;

                                            //  Пациент перемещается в смотровую комнату
                                            form.Invoke(PatientEntryObservationRoom, new Object[] { patient });
                                            form._observationRoomQueue.Add(patient);

                                            
                                            str = "---- Пациент " + patient.Number.ToString() + " перешел в смотровую комнату в " +
                                                DateTime.Now.ToString() + "\n";
                                            form.Invoke(form.ToProtocolDelegate, new Object[] { str });
                                        }
                                        else
                                        {
                                            //  Увеличим номер пациента в очереди
                                            i++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //  Если нет команды завершить работу
                if (!isExit)
                {
                    //  Ждем новых пацинтов
                    Thread.Sleep(1000);
                }
            }

            
            str = "Смотровая комната закончила работу в " + DateTime.Now.ToString() + "\n";
            form.Invoke(form.ToProtocolDelegate, new Object[] { str });

            //  Удаляем ссылку на поток из списка
            form.Invoke(form.RemoveCompletedThread, new Object[] { form._observationRoomThread });
        }
    }
}
