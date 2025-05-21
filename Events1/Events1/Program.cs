using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WaterSupplyGraphicalSimulation
{
    public enum WaterTowerState { Normal, Low, Empty, Full } // ����� ���������� ���
    // ���� ���������� ���
    public class WaterTower
    {
        public int CurrentVolume { get; private set; } // �������� ��'�� ���� (��������� ���� ���������)
        public int MaxVolume { get; } // ����������� ������
        public event Action<WaterTowerState> StateChanged; // ���� ���� �����

        public WaterTower(int maxVolume) 
        {
            MaxVolume = maxVolume;
            CurrentVolume = maxVolume / 2; // ���������� ��'�� � 50%
        }

        public void Consume(int amount) // ���������� ����
        {
            CurrentVolume = Math.Max(0, CurrentVolume - amount); // �� ���� ���� ����� 0
            UpdateState(); // ��������� ����� ���� ���
        }

        public void AddWater(int amount) // ��������� ����
        {
            if (CurrentVolume < MaxVolume) //���� �������� �ᒺ� ������ �� ������������
            {
                CurrentVolume = Math.Min(MaxVolume, CurrentVolume + amount); // �� �������� �ᒺ� ������� ������
                UpdateState(); // ��������� �����
            }
        }

        private void UpdateState() // ��������� ����� ���
        {
            var newState = CurrentVolume == 0 ? WaterTowerState.Empty :
                          CurrentVolume < MaxVolume * 0.2 ? WaterTowerState.Low :
                          CurrentVolume >= MaxVolume * 0.95 ? WaterTowerState.Full :
                          WaterTowerState.Normal;
            StateChanged?.Invoke(newState);
        }
    }

    // ����������� ���� �����
    public abstract class Pump
    {
        public Point Position { get; protected set; } // ������� � ���������� ���
        public bool IsActive { get; protected set; } // �� �������� �����
        public int FlowRate { get; } // ������������� (�/��)
        public string Status { get; protected set; } = "���������"; // ������
        public event Action<int> PumpedWater; // ���� �������� ����

        // ����������� � ����'�������� �����������
        protected Pump(Point position, int flowRate)
        {
            Position = position;
            FlowRate = flowRate;
        }

        public abstract void Update(WaterTowerState state); // ����������� ����� ���������
        protected void PumpWater() => PumpedWater?.Invoke(FlowRate); // ������ ��䳿 �������� ����
    }

    // ���� ����� �����
    public class ManualPump : Pump
    {
        public ManualPump(Point position, int flowRate) : base(position, flowRate) { }

        public override void Update(WaterTowerState state)
        {
            IsActive = state == WaterTowerState.Low || state == WaterTowerState.Empty; // ������� ��� �������� ��� ��� ���� ����� ����
            Status = IsActive ? "������" : "���������";
            if (IsActive) PumpWater(); // �������� ����, ���� �������
        }
    }

    // ���� ���������� �����
    public class ElectricPump : Pump
    {
        private int _overheat; // г���� �������� (0-100)
        public bool IsOverheated { get; private set; } // �� ��������

        public ElectricPump(Point position, int flowRate) : base(position, flowRate) { }

        public override void Update(WaterTowerState state)
        {
            if (IsOverheated)
            {
                Status = "����������!"; // ���������� ������, ���� ����������
                return;
            }

            IsActive = state == WaterTowerState.Low || state == WaterTowerState.Empty; // ������� ��� �������� ���
            Status = IsActive ? "������" : "���������";

            if (IsActive) // ���� �������
            {
                PumpWater();
                _overheat += 10; // �� ��������� ��������
                if (_overheat >= 100) Overheat(); // ������� ��� 100
            }
            else
            {
                _overheat = Math.Max(0, _overheat - 5); // �����������
            }
        }

        private void Overheat() // ������� ��������
        {
            IsOverheated = true;
            Status = "����������!";
            var timer = new System.Windows.Forms.Timer { Interval = 100 }; // ������ ��� ��������
            timer.Tick += (s, e) =>
            {
                // �������� ��������� ��������
                _overheat = 0; // г���� �������� �����������
                IsOverheated = false; // ������ �������� ��������
                Status = "���������"; // ��������� �������
                timer.Stop(); // ������� ������� ���� ����������
            };
            timer.Start();
        }
    }
    // ���� ��������� ����
    public class Consumer
    {
        public Point Position { get; } // ������� �������
        private readonly WaterTower _tower; // ��������� �� ����
        private readonly int _consumption; // ��'�� ����������
        private bool _canConsume = true; // �� ����� ���������

        public Consumer(Point position, WaterTower tower, int consumption)
        {
            Position = position;
            _tower = tower;
            _consumption = consumption;
            tower.StateChanged += state => _canConsume = state != WaterTowerState.Empty; // ���������� ��� ������� ���
        }

        public void Update()
        {
            if (_canConsume) _tower.Consume(_consumption); // ���������� ����
        }
    }

    public class MainForm : Form
    {
        private readonly WaterTower _tower = new WaterTower(1000); // ���� � ������ 1000 �
        private readonly List<Pump> _pumps = new List<Pump>(); // ������ ������
        private readonly List<Consumer> _consumers = new List<Consumer>(); // ������ ����������
        private readonly System.Windows.Forms.Timer _simTimer = new System.Windows.Forms.Timer { Interval = 500 }; // ������ ���������
        private int _time; // ��� � ��������
        private WaterTowerState _currentState; // �������� ���� ���
        private string _pumpStatus = "����� �������"; // ������ ����
        private string _waterFlowStatus = "���������� ����"; // ������ ������

        public MainForm()
        {
            DoubleBuffered = true;
            ClientSize = new Size(1000, 600); // ����� ����
            Text = "�������� 2 � ��������������";

            // ����������� ����
            _pumps.Add(new ManualPump(new Point(100, 450), 5));  // ������ ����� (��������� 5 �/��)
            _pumps.Add(new ElectricPump(new Point(250, 450), 250)); // ���������� ����� (��������� 250 �/��)

            // ����������� ����������
            _consumers.Add(new Consumer(new Point(700, 380), _tower, 50)); // �������� (���������� ���� 50 �)
            _consumers.Add(new Consumer(new Point(850, 380), _tower, 70)); // �������� (���������� ���� 70 �)

            // ϳ������ �� ��䳿
            foreach (var pump in _pumps)
            {
                pump.PumpedWater += amount =>
                {
                    _tower.AddWater(amount); // ����������� ��������� ����
                    UpdatePumpStatus(pump);  // ��������� �������
                    Invalidate(); // ������������� �����
                };
            }

            // ������������ �������
            _simTimer = new System.Windows.Forms.Timer { Interval = 1000 }; // 1 ������� = 1 �������
            _simTimer.Tick += (s, e) =>
            {
                _time += 1; // ��������� ���� �� ����� ��
                foreach (var consumer in _consumers) consumer.Update(); // ���������� ����
                Invalidate(); // �������������
            };

            var startBtn = new Button // ������ ������
            {
                Text = "������ ��䳿",
                Location = new Point(400, 550),
                Size = new Size(120, 30)
            };
            startBtn.Click += (s, e) => _simTimer.Enabled = !_simTimer.Enabled; // ��������/�������� ������
            Controls.Add(startBtn); // ��������� ������ �� �����

            _tower.StateChanged += state => // ϳ������ �� ���� ����� ���
            {
                _currentState = state;
                UpdateStatusMessages(); // ��������� �������
                Invalidate();
            };

            foreach (var pump in _pumps) 
            {
                // ��������� ������� ��� ����������� ������� ������
                pump.PumpedWater += amount =>
                {
                    _pumpStatus = pump is ElectricPump
                        ? $"��. �����: {(((ElectricPump)pump).IsOverheated ? "�����в�" : "�������")}"
                        : "����� ����� �������";
                    _waterFlowStatus = "���������� ����������";
                    Invalidate();
                };
            } 

        }

        private void UpdatePumpStatus(Pump pump) // ��������� ������� �����
        {
            _pumpStatus = pump switch
            {
                ElectricPump electricPump =>
                    $"����������: {(electricPump.IsOverheated ? "�����в�" : "�������")} ({electricPump.FlowRate} �/��)",
                ManualPump manualPump =>
                    $"�����: {(manualPump.IsActive ? "������" : "���������")} ({manualPump.FlowRate} �/��)",
                _ => "������� �����"
            };
        }

        private void UpdateStatusMessages() // ��������� ��������� �������
        {
            switch (_currentState)
            {
                case WaterTowerState.Empty:
                    _waterFlowStatus = "��������: ��������� �������!";
                    // ��������� ���������� �� �����
                    foreach (var pump in _pumps)
                    {
                        if (pump is ElectricPump electricPump && electricPump.IsOverheated)
                            continue;
                        pump.Update(WaterTowerState.Empty);
                    }
                    break;
                case WaterTowerState.Low:
                    _waterFlowStatus = "������� ����� ����";
                    _pumpStatus = "����� ������";
                    break;
                case WaterTowerState.Normal:
                    _waterFlowStatus = "���������� �����";
                    _pumpStatus = "����� �� ���������";
                    break;
                case WaterTowerState.Full:
                    _waterFlowStatus = "��������� ����������";
                    _pumpStatus = "����� �������";
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e) // ³��������� �����
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias; // ������������ �������

            // ���
            DrawSkyAndGround(g);

            // ���������� ����
            DrawWaterTower(g, new Rectangle(400, 150, 120, 300));

            // �����
            DrawPipes(g);

            // �����
            foreach (var pump in _pumps)
                DrawPump(g, pump);

            // ���������
            foreach (var consumer in _consumers)
                DrawHouse(g, consumer.Position);

            // ����������
            DrawInfoPanel(g);
            DrawStatusPanel(g);
        }

        private void DrawSkyAndGround(Graphics g) // ��������� ���� �� ����
        {
            // ���䳺���� ����
            using (var skyBrush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(0, Height),
                Color.LightSkyBlue,
                Color.DeepSkyBlue))
            {
                g.FillRectangle(skyBrush, ClientRectangle);
            }

            // �����
            using (var groundTexture = new HatchBrush(HatchStyle.DashedHorizontal, Color.DarkOliveGreen, Color.SaddleBrown))
            {
                g.FillRectangle(groundTexture, 0, 450, Width, 150);
            }
        }

        private void DrawWaterTower(Graphics g, Rectangle rect) // ��������� ���������� ���
        {
            // ������ ���
            using (var towerBrush = new LinearGradientBrush(rect, Color.SteelBlue, Color.DarkSlateBlue, 90))
            {
                g.FillRectangle(towerBrush, rect);
            }

            // г���� ����
            int waterHeight = (int)(rect.Height * ((double)_tower.CurrentVolume / _tower.MaxVolume));

            // ��������, ��� �������� ��������� ������������ � �������� �������
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

            // ��� �� �����
            g.DrawRectangle(new Pen(Color.DarkBlue, 3), rect);
            g.FillPolygon(Brushes.SlateGray, new[] {
                new Point(rect.X - 15, rect.Y),
                new Point(rect.X + rect.Width + 15, rect.Y),
                new Point(rect.X + rect.Width/2, rect.Y - 30)
            });
        }

        private void DrawPipes(Graphics g) // ��������� ����
        {
            using (var pipePen = new Pen(Color.Gray, 15))
            {
                // ����������� ����� �� ���
                g.DrawLine(pipePen, 460, 450, 460, 300);

                // ������������ �����
                g.DrawLine(pipePen, 460, 450, 700, 450);
                g.DrawLine(pipePen, 700, 450, 850, 450);
            }
        }

        private void DrawPump(Graphics g, Pump pump) // ��������� �����
        {
            Color baseColor = Color.Gray;
            if (pump is ManualPump)
                baseColor = pump.IsActive ? Color.DarkGreen : Color.Gray;
            else if (pump is ElectricPump electricPump)
                baseColor = electricPump.IsOverheated ? Color.Red :
                          pump.IsActive ? Color.LimeGreen : Color.Gray;

            // ������ �����
            var pumpRect = new Rectangle(pump.Position.X, pump.Position.Y, 100, 60);
            using (var pumpBrush = new LinearGradientBrush(pumpRect, baseColor, ControlPaint.Dark(baseColor), 45))
            {
                g.FillRectangle(pumpBrush, pumpRect);
            }
            g.DrawRectangle(new Pen(Color.Black, 2), pumpRect);

            // �����
            g.FillEllipse(Brushes.Black, pump.Position.X + 20, pump.Position.Y + 40, 25, 25);
            g.FillEllipse(Brushes.Black, pump.Position.X + 55, pump.Position.Y + 40, 25, 25);
            g.FillRectangle(Brushes.Silver, pump.Position.X + 10, pump.Position.Y + 10, 80, 20); // ������

            // ϳ������
            if (pump.IsActive)
            {
                using (var glowPen = new Pen(Color.FromArgb(100, Color.Yellow), 8))
                {
                    g.DrawRectangle(glowPen, pumpRect);
                }
            }

            // ϳ����
            g.DrawString(pump is ManualPump ? "�����" : "����������",
                new Font("Arial", 9, FontStyle.Bold), Brushes.White,
                pump.Position.X + 10, pump.Position.Y + 65);
        }

        private void DrawHouse(Graphics g, Point pos) // ��������� �������
        {
            // ������ �������
            var houseRect = new Rectangle(pos.X - 50, 300, 100, 150);
            using (var houseBrush = new LinearGradientBrush(houseRect, Color.Tan, Color.Peru, 90))
            {
                g.FillRectangle(houseBrush, houseRect);
            }

            // ���
            g.FillPolygon(Brushes.DarkRed, new[] {
                new Point(pos.X - 50, 300),
                new Point(pos.X, 250),
                new Point(pos.X + 50, 300)
            });

            // ³���
            g.FillRectangle(Brushes.LightGoldenrodYellow, pos.X - 40, 320, 30, 40);
            g.FillRectangle(Brushes.LightGoldenrodYellow, pos.X + 10, 320, 30, 40);
            g.DrawRectangles(Pens.Black, new[] {
                new Rectangle(pos.X - 40, 320, 30, 40),
                new Rectangle(pos.X + 10, 320, 30, 40)
            });

            // ����
            g.FillRectangle(Brushes.Sienna, pos.X - 15, 380, 30, 70);
            g.DrawRectangle(Pens.Black, pos.X - 15, 380, 30, 70);
        }

        private void DrawInfoPanel(Graphics g) // ��������� ������������ �����
        {
            var infoRect = new Rectangle(10, 50, 200, 80);
            using (var panelBrush = new SolidBrush(Color.FromArgb(200, Color.White)))
            {
                g.FillRectangle(panelBrush, infoRect);
            }
            g.DrawRectangle(Pens.Gray, infoRect);

            g.DrawString($"���: {_time} ��\n����: {_tower.CurrentVolume}/{_tower.MaxVolume} �",
                new Font("Arial", 10), Brushes.Black, 15, 55);
        }

        private void DrawStatusPanel(Graphics g) // ��������� ����� �������
        {
            var statusRect = new Rectangle(Width - 260, 10, 250, 120);
            using (var panelBrush = new SolidBrush(Color.FromArgb(220, Color.LightGray)))
            {
                g.FillRectangle(panelBrush, statusRect);
            }
            g.DrawRectangle(Pens.DarkGray, statusRect);

            var warningColor = _currentState == WaterTowerState.Empty ? Color.Red : Color.Black;
            var lines = new[] {
                "���� �������:",
                $"���������: {GetStateName(_currentState)}",
                $"г����: {_tower.CurrentVolume}/{_tower.MaxVolume} �",
                $"����: {_waterFlowStatus}",
                $"������ ����: {_pumpStatus}"
        };

            var y = 15;
            foreach (var line in lines)
            {
                g.DrawString(line,
                    new Font("Arial", line.StartsWith("����") ? 10 : 9,
                        line.StartsWith("����") ? FontStyle.Bold : FontStyle.Regular),
                    line.Contains("��������") ? Brushes.Red : Brushes.Black,
                    Width - 255, y);
                y += 20;
            }
        }

        private string GetStateName(WaterTowerState state)
        {
            return state switch
            {
                WaterTowerState.Empty => "������� (��������!)",
                WaterTowerState.Low => "������� �����",
                WaterTowerState.Normal => "����������",
                WaterTowerState.Full => "����������",
                _ => "��������"
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