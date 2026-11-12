using System;
using System.Collections.Generic;
using System.Globalization;
using stewart_platform.Control;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using stewart_platform.Services;
using stewart_platform.Visualization;

namespace stewart_platform
{
    public partial class MainWindow : Window
    {
        // Existing platform configuration and kinematics are not modified.
        private readonly RobotConfig config = new RobotConfig();
        private readonly StewartPlatform platform;

        private readonly PlatformConnectionService connection = new PlatformConnectionService();
        private readonly PlatformMovementService movement = new PlatformMovementService();
        private readonly StewartVisualizer visualizer;
        private readonly DispatcherTimer sendTimer;
        private readonly DispatcherTimer movementTimer;
        private readonly double[] targetValues = new double[6]; // X, Y, Z, Rx, Ry, Rz
        private bool isInternalUpdate;
        private bool isClosedLoop;
        private bool hasFeedback;
        private DateTime lastFeedbackUtc = DateTime.MinValue;
        private double feedbackRoll, feedbackPitch;
        private DateTime lastControllerUpdateUtc = DateTime.MinValue;
        private readonly DispatcherTimer controlTimer;
        private readonly PlatformLevelController levelController = new PlatformLevelController();
        private const double MaximumSafeRotationDegrees = 8.0;
        private bool isSending;
        private bool transmissionInhibited;

        // Closed-loop is a one-time leveling procedure, not a permanent servo governor.
        private enum LevelingPhase { OpenLoop, ReturningToZero, Leveling, ManualReady }
        private LevelingPhase levelingPhase = LevelingPhase.OpenLoop;
        private double heldRollTrim;
        private double heldPitchTrim;


        public MainWindow()
        {
            InitializeComponent();

            platform = new StewartPlatform(config);
            SetupSliders();

            visualizer = new StewartVisualizer(
                VisBase, VisPlatform,
                new List<TubeVisual3D> { VisHorn0, VisHorn1, VisHorn2, VisHorn3, VisHorn4, VisHorn5 },
                new List<TubeVisual3D> { VisRod0, VisRod1, VisRod2, VisRod3, VisRod4, VisRod5 });

            connection.FeedbackLineReceived += ProcessFeedbackLine;
            LoadAvailablePorts();

            sendTimer = new DispatcherTimer();
            sendTimer.Interval = TimeSpan.FromMilliseconds(40);
            sendTimer.Tick += SendDataToPlatform;

            movementTimer = new DispatcherTimer();
            movementTimer.Interval = TimeSpan.FromMilliseconds(20);
            movementTimer.Tick += MovementTimer_Tick;

            controlTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            controlTimer.Tick += ControlTimer_Tick;
            controlTimer.Start();

            platform.ApplyTranslationAndRotation(0, 0, 0, 0, 0, 0);
            Update3DVisualization();
            UpdateModeUI();
        }

        private void SetupSliders()
        {
            SldPosX.Minimum = -config.MaxTranslation; SldPosX.Maximum = config.MaxTranslation;
            SldPosY.Minimum = -config.MaxTranslation; SldPosY.Maximum = config.MaxTranslation;
            SldPosZ.Minimum = -config.MaxTranslation; SldPosZ.Maximum = config.MaxTranslation;

            SldRotX.Minimum = -config.MaxRotation; SldRotX.Maximum = config.MaxRotation;
            SldRotY.Minimum = -config.MaxRotation; SldRotY.Maximum = config.MaxRotation;
            SldRotZ.Minimum = -config.MaxRotation; SldRotZ.Maximum = config.MaxRotation;
        }

    }
}
