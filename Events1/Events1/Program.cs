using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WaterSupplyGraphicalSimulation
{
    public enum WaterTowerState { Normal, Low, Empty, Full } // Стани водонапірної вежі
    // Клас водонапірної вежі
    public class WaterTower
    {
        public int CurrentVolume { get; private set; } // Поточний об'єм води (змінюється лише внутрішньо)
        public int MaxVolume { get; } // Максимальна ємність
        public event Action<WaterTowerState> StateChanged; // Подія зміни стану

        public WaterTower(int maxVolume) 
        {
            MaxVolume = maxVolume;
            CurrentVolume = maxVolume / 2; // Початковий об'єм — 50%
        }

        public void Consume(int amount) // Споживання води
        {
            CurrentVolume = Math.Max(0, CurrentVolume - amount); // Не може бути менше 0
            UpdateState(); // Оновлення стану після змін
        }

        public void AddWater(int amount) // Додавання води
        {
            if (CurrentVolume < MaxVolume) //Якщо поточний об’єм менший за максимальний
            {
                CurrentVolume = Math.Min(MaxVolume, CurrentVolume + amount); // то поточний об’єм дорівнює мінімуму
                UpdateState(); // Оновлення стану
            }
        }

        private void UpdateState() // Оновлення стану вежі
        {
            var newState = CurrentVolume == 0 ? WaterTowerState.Empty :
                          CurrentVolume < MaxVolume * 0.2 ? WaterTowerState.Low :
                          CurrentVolume >= MaxVolume * 0.95 ? WaterTowerState.Full :
                          WaterTowerState.Normal;
            StateChanged?.Invoke(newState);
        }
    }

    // Абстрактний клас помпи
    public abstract class Pump
    {
        public Point Position { get; protected set; } // Позиція у графічному вікні
        public bool IsActive { get; protected set; } // Чи активний насос
        public int FlowRate { get; } // Продуктивність (л/хв)
        public string Status { get; protected set; } = "Неактивна"; // Статус
        public event Action<int> PumpedWater; // Подія передачі води

        // Конструктор з обов'язковими параметрами
        protected Pump(Point position, int flowRate)
        {
            Position = position;
            FlowRate = flowRate;
        }

        public abstract void Update(WaterTowerState state); // Абстрактний метод оновлення
        protected void PumpWater() => PumpedWater?.Invoke(FlowRate); // Виклик події передачі води
    }

    // Клас Ручна помпа
    public class ManualPump : Pump
    {
        public ManualPump(Point position, int flowRate) : base(position, flowRate) { }

        public override void Update(WaterTowerState state)
        {
            IsActive = state == WaterTowerState.Low || state == WaterTowerState.Empty; // Активна при низькому рівні або коли пуста вежа
            Status = IsActive ? "Працює" : "Неактивна";
            if (IsActive) PumpWater(); // Передача води, якщо активна
        }
    }

    // Клас електрична помпа
    public class ElectricPump : Pump
    {
        private int _overheat; // Рівень перегріву (0-100)
        public bool IsOverheated { get; private set; } // Чи перегріта

        public ElectricPump(Point position, int flowRate) : base(position, flowRate) { }

        public override void Update(WaterTowerState state)
        {
            if (IsOverheated)
            {
                Status = "Перегрілась!"; // Блокування роботи, якщо перегрілась
                return;
            }

            IsActive = state == WaterTowerState.Low || state == WaterTowerState.Empty; // Активна при низькому рівні
            Status = IsActive ? "Працює" : "Неактивна";

            if (IsActive) // Якщо активна
            {
                PumpWater();
                _overheat += 10; // то збільшення перегріву
                if (_overheat >= 100) Overheat(); // Перегрів при 100
            }
            else
            {
                _overheat = Math.Max(0, _overheat - 5); // Охолодження
            }
        }

        private void Overheat() // Обробка перегріву
        {
            IsOverheated = true;
            Status = "Перегрілась!";
            var timer = new System.Windows.Forms.Timer { Interval = 100 }; // Таймер для скидання
            timer.Tick += (s, e) =>
            {
                // Скидання параметрів перегріву
                _overheat = 0; // Рівень перегріву обнуляється
                IsOverheated = false; // Зняття прапорця перегріву
                Status = "Неактивна"; // Оновлення статусу
                timer.Stop(); // Зупинка таймера після завершення
            };
            timer.Start();
        }
    }
    // Клас споживача води
    public class Consumer
    {
        public Point Position { get; } // Позиція будинку
        private readonly WaterTower _tower; // Посилання на вежу
        private readonly int _consumption; // Об'єм споживання
        private bool _canConsume = true; // Чи можна споживати

        public Consumer(Point position, WaterTower tower, int consumption)
        {
            Position = position;
            _tower = tower;
            _consumption = consumption;
            tower.StateChanged += state => _canConsume = state != WaterTowerState.Empty; // Блокування при порожній вежі
        }

        public void Update()
        {
            if (_canConsume) _tower.Consume(_consumption); // Споживання води
        }
    }

    public class MainForm : Form
    {
        private readonly WaterTower _tower = new WaterTower(1000); // Вежа з ємністю 1000 л
        private readonly List<Pump> _pumps = new List<Pump>(); // Список насосів
        private readonly List<Consumer> _consumers = new List<Consumer>(); // Список споживачів
        private readonly System.Windows.Forms.Timer _simTimer = new System.Windows.Forms.Timer { Interval = 500 }; // Таймер симуляції
        private int _time; // Час у хвилинах
        private WaterTowerState _currentState; // Поточний стан вежі
        private string _pumpStatus = "Помпи вимкнені"; // Статус помп
        private string _waterFlowStatus = "Нормальний потік"; // Статус потоку

        public MainForm()
        {
            DoubleBuffered = true;
            ClientSize = new Size(1000, 600); // Розмір вікна
            Text = "Завдання 2 – Водопостачання";

            // Ініціалізація помп
            _pumps.Add(new ManualPump(new Point(100, 450), 5));  // Ручниа помпа (потужність 5 л/хв)
            _pumps.Add(new ElectricPump(new Point(250, 450), 250)); // Електрична помпа (потужність 250 л/хв)

            // Ініціалізація споживачів
            _consumers.Add(new Consumer(new Point(700, 380), _tower, 50)); // споживач (споживання води 50 л)
            _consumers.Add(new Consumer(new Point(850, 380), _tower, 70)); // споживач (споживання води 70 л)

            // Підписка на події
            foreach (var pump in _pumps)
            {
                pump.PumpedWater += amount =>
                {
                    _tower.AddWater(amount); // Безпосереднє додавання води
                    UpdatePumpStatus(pump);  // Оновлення статусу
                    Invalidate(); // Перемалювання форми
                };
            }

            // Налаштування таймера
            _simTimer = new System.Windows.Forms.Timer { Interval = 1000 }; // 1 секунда = 1 хвилина
            _simTimer.Tick += (s, e) =>
            {
                _time += 1; // Оновлення часу на кожен тік
                foreach (var consumer in _consumers) consumer.Update(); // Споживання води
                Invalidate(); // Перемалювання
            };

            var startBtn = new Button // Кнопка старту
            {
                Text = "Почати події",
                Location = new Point(400, 550),
                Size = new Size(120, 30)
            };
            startBtn.Click += (s, e) => _simTimer.Enabled = !_simTimer.Enabled; // Включити/вимкнути таймер
            Controls.Add(startBtn); // Додавання кнопки на форму

            _tower.StateChanged += state => // Підписка на зміну стану вежі
            {
                _currentState = state;
                UpdateStatusMessages(); // Оновлення статусів
                Invalidate();
            };

            foreach (var pump in _pumps) 
            {
                // Додаткова підписка для відображення статусів насосів
                pump.PumpedWater += amount =>
                {
                    _pumpStatus = pump is ElectricPump
                        ? $"Ел. помпа: {(((ElectricPump)pump).IsOverheated ? "ПЕРЕГРІВ" : "Активна")}"
                        : "Ручна помпа активна";
                    _waterFlowStatus = "Наповнення резервуару";
                    Invalidate();
                };
            } 

        }

        private void UpdatePumpStatus(Pump pump) // Оновлення статусу помпи
        {
            _pumpStatus = pump switch
            {
                ElectricPump electricPump =>
                    $"Електрична: {(electricPump.IsOverheated ? "ПЕРЕГРІВ" : "Активна")} ({electricPump.FlowRate} л/хв)",
                ManualPump manualPump =>
                    $"Ручна: {(manualPump.IsActive ? "Працює" : "Неактивна")} ({manualPump.FlowRate} л/хв)",
                _ => "Невідома помпа"
            };
        }

        private void UpdateStatusMessages() // Оновлення текстових статусів
        {
            switch (_currentState)
            {
                case WaterTowerState.Empty:
                    _waterFlowStatus = "КРИТИЧНО: резервуар порожній!";
                    // Примусово активувати всі помпи
                    foreach (var pump in _pumps)
                    {
                        if (pump is ElectricPump electricPump && electricPump.IsOverheated)
                            continue;
                        pump.Update(WaterTowerState.Empty);
                    }
                    break;
                case WaterTowerState.Low:
                    _waterFlowStatus = "Низький рівень води";
                    _pumpStatus = "Помпи активні";
                    break;
                case WaterTowerState.Normal:
                    _waterFlowStatus = "Нормальний режим";
                    _pumpStatus = "Помпи на підготовці";
                    break;
                case WaterTowerState.Full:
                    _waterFlowStatus = "Резервуар заповнений";
                    _pumpStatus = "Помпи вимкнені";
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e) // Відмальовка форми
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias; // Згладжування графіки

            // Фон
            DrawSkyAndGround(g);

            // Водонапірна вежа
            DrawWaterTower(g, new Rectangle(400, 150, 120, 300));

            // Труби
            DrawPipes(g);

            // Помпи
            foreach (var pump in _pumps)
                DrawPump(g, pump);

            // Споживачі
            foreach (var consumer in _consumers)
                DrawHouse(g, consumer.Position);

            // Інформація
            DrawInfoPanel(g);
            DrawStatusPanel(g);
        }

        private void DrawSkyAndGround(Graphics g) // Малювання неба та землі
        {
            // Градієнтне небо
            using (var skyBrush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(0, Height),
                Color.LightSkyBlue,
                Color.DeepSkyBlue))
            {
                g.FillRectangle(skyBrush, ClientRectangle);
            }

            // Земля
            using (var groundTexture = new HatchBrush(HatchStyle.DashedHorizontal, Color.DarkOliveGreen, Color.SaddleBrown))
            {
                g.FillRectangle(groundTexture, 0, 450, Width, 150);
            }
        }

        private void DrawWaterTower(Graphics g, Rectangle rect) // Малювання водонапірної вежі
        {
            // Основа вежі
            using (var towerBrush = new LinearGradientBrush(rect, Color.SteelBlue, Color.DarkSlateBlue, 90))
            {
                g.FillRectangle(towerBrush, rect);
            }

            // Рівень води
            int waterHeight = (int)(rect.Height * ((double)_tower.CurrentVolume / _tower.MaxVolume));

            // Перевірка, щоб уникнути створення прямокутника з нульовою висотою
            if (waterHeight > 0)
            {
                using (var waterBrush = new LinearGradientBrush(
                    new Rectangle(rect.X, rect.Y + rect.Height - waterHeight, rect.Width, waterHeight),
                    Color.DodgerBlue,
                    Color.RoyalBlue,
                    90))
                {
                    g.FillRectangle(waterBrush, rect.X, rect.Y + rect.Height - waterHeight, rect.Width, waterHeight);
                }
            }

            // Дах та деталі
            g.DrawRectangle(new Pen(Color.DarkBlue, 3), rect);
            g.FillPolygon(Brushes.SlateGray, new[] {
                new Point(rect.X - 15, rect.Y),
                new Point(rect.X + rect.Width + 15, rect.Y),
                new Point(rect.X + rect.Width/2, rect.Y - 30)
            });
        }

        private void DrawPipes(Graphics g) // Малювання труб
        {
            using (var pipePen = new Pen(Color.Gray, 15))
            {
                // Вертикальна труба до вежі
                g.DrawLine(pipePen, 460, 450, 460, 300);

                // Горизонтальні труби
                g.DrawLine(pipePen, 460, 450, 700, 450);
                g.DrawLine(pipePen, 700, 450, 850, 450);
            }
        }

        private void DrawPump(Graphics g, Pump pump) // Малювання помпи
        {
            Color baseColor = Color.Gray;
            if (pump is ManualPump)
                baseColor = pump.IsActive ? Color.DarkGreen : Color.Gray;
            else if (pump is ElectricPump electricPump)
                baseColor = electricPump.IsOverheated ? Color.Red :
                          pump.IsActive ? Color.LimeGreen : Color.Gray;

            // Основа помпи
            var pumpRect = new Rectangle(pump.Position.X, pump.Position.Y, 100, 60);
            using (var pumpBrush = new LinearGradientBrush(pumpRect, baseColor, ControlPaint.Dark(baseColor), 45))
            {
                g.FillRectangle(pumpBrush, pumpRect);
            }
            g.DrawRectangle(new Pen(Color.Black, 2), pumpRect);

            // Деталі
            g.FillEllipse(Brushes.Black, pump.Position.X + 20, pump.Position.Y + 40, 25, 25);
            g.FillEllipse(Brushes.Black, pump.Position.X + 55, pump.Position.Y + 40, 25, 25);
            g.FillRectangle(Brushes.Silver, pump.Position.X + 10, pump.Position.Y + 10, 80, 20); // Панель

            // Підсвітка
            if (pump.IsActive)
            {
                using (var glowPen = new Pen(Color.FromArgb(100, Color.Yellow), 8))
                {
                    g.DrawRectangle(glowPen, pumpRect);
                }
            }

            // Підпис
            g.DrawString(pump is ManualPump ? "Ручна" : "Електрична",
                new Font("Arial", 9, FontStyle.Bold), Brushes.White,
                pump.Position.X + 10, pump.Position.Y + 65);
        }

        private void DrawHouse(Graphics g, Point pos) // Малювання будинку
        {
            // Основа будинку
            var houseRect = new Rectangle(pos.X - 50, 300, 100, 150);
            using (var houseBrush = new LinearGradientBrush(houseRect, Color.Tan, Color.Peru, 90))
            {
                g.FillRectangle(houseBrush, houseRect);
            }

            // Дах
            g.FillPolygon(Brushes.DarkRed, new[] {
                new Point(pos.X - 50, 300),
                new Point(pos.X, 250),
                new Point(pos.X + 50, 300)
            });

            // Вікна
            g.FillRectangle(Brushes.LightGoldenrodYellow, pos.X - 40, 320, 30, 40);
            g.FillRectangle(Brushes.LightGoldenrodYellow, pos.X + 10, 320, 30, 40);
            g.DrawRectangles(Pens.Black, new[] {
                new Rectangle(pos.X - 40, 320, 30, 40),
                new Rectangle(pos.X + 10, 320, 30, 40)
            });

            // Двері
            g.FillRectangle(Brushes.Sienna, pos.X - 15, 380, 30, 70);
            g.DrawRectangle(Pens.Black, pos.X - 15, 380, 30, 70);
        }

        private void DrawInfoPanel(Graphics g) // Малювання інформаційної панелі
        {
            var infoRect = new Rectangle(10, 50, 200, 80);
            using (var panelBrush = new SolidBrush(Color.FromArgb(200, Color.White)))
            {
                g.FillRectangle(panelBrush, infoRect);
            }
            g.DrawRectangle(Pens.Gray, infoRect);

            g.DrawString($"Час: {_time} хв\nВода: {_tower.CurrentVolume}/{_tower.MaxVolume} л",
                new Font("Arial", 10), Brushes.Black, 15, 55);
        }

        private void DrawStatusPanel(Graphics g) // Малювання панелі статусу
        {
            var statusRect = new Rectangle(Width - 260, 10, 250, 120);
            using (var panelBrush = new SolidBrush(Color.FromArgb(220, Color.LightGray)))
            {
                g.FillRectangle(panelBrush, statusRect);
            }
            g.DrawRectangle(Pens.DarkGray, statusRect);

            var warningColor = _currentState == WaterTowerState.Empty ? Color.Red : Color.Black;
            var lines = new[] {
                "СТАН СИСТЕМИ:",
                $"Резервуар: {GetStateName(_currentState)}",
                $"Рівень: {_tower.CurrentVolume}/{_tower.MaxVolume} л",
                $"Потік: {_waterFlowStatus}",
                $"Статус помп: {_pumpStatus}"
        };

            var y = 15;
            foreach (var line in lines)
            {
                g.DrawString(line,
                    new Font("Arial", line.StartsWith("СТАН") ? 10 : 9,
                        line.StartsWith("СТАН") ? FontStyle.Bold : FontStyle.Regular),
                    line.Contains("КРИТИЧНО") ? Brushes.Red : Brushes.Black,
                    Width - 255, y);
                y += 20;
            }
        }

        private string GetStateName(WaterTowerState state)
        {
            return state switch
            {
                WaterTowerState.Empty => "Порожній (КРИТИЧНО!)",
                WaterTowerState.Low => "Низький рівень",
                WaterTowerState.Normal => "Нормальний",
                WaterTowerState.Full => "Заповнений",
                _ => "Невідомий"
            };
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}