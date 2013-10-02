using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Questions
{
    public sealed partial class EasterEggPage : Page
    {
        private KeyEventHandler keyUpHandler;

        private bool initialized = false;

        private double speedX;
        private double speedY;

        private double positionX;
        private double positionY;

        private Rectangle rectangle;
        private Accelerometer accelerometer;

        public EasterEggPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            positionX = TheCanvas.ActualWidth / 2;
            positionY = TheCanvas.Height / 2;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += OnTick;

            accelerometer = Accelerometer.GetDefault();
            if (accelerometer != null)
            {
                uint interval = accelerometer.MinimumReportInterval * 2;
                Debug.WriteLine("Interval: " + interval);

                timer.Interval = new TimeSpan(0, 0, 0, 0, (int)interval);

                timer.Start();
            }

            RegisterShortcuts();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            UnregisterShortcuts();
        }

        private void RegisterShortcuts()
        {
            // I don't know why, but assign the hanlder to the page does not work. But this does.
            keyUpHandler = new KeyEventHandler(Page_KeyUp);
            Window.Current.Content.AddHandler(UIElement.KeyUpEvent, keyUpHandler, false);
        }

        private void UnregisterShortcuts()
        {
            Window.Current.Content.RemoveHandler(UIElement.KeyUpEvent, keyUpHandler);
        }

        private void OnTick(object sender, object e)
        {
            Initialize();

            if (accelerometer != null)
            {
                AccelerometerReading reading = accelerometer.GetCurrentReading();

                if (reading != null)
                {
                    //Debug.WriteLine(reading.AccelerationX + ", " + reading.AccelerationY);

                    // Increment speed and move object.
                    speedX += reading.AccelerationX;
                    positionX += speedX;

                    if (positionX < 0)
                    {
                        positionX = 0;

                        // Invert direction.
                        speedX *= -1;
                    }
                    else if (positionX > TheCanvas.ActualWidth - rectangle.Width)
                    {
                        positionX = TheCanvas.ActualWidth - rectangle.Width;

                        // Invert direction.
                        speedX *= -1;
                    }

                    // Increment speed and move object.
                    speedY += reading.AccelerationY;
                    positionY -= speedY;

                    if (positionY < 0)
                    {
                        positionY = 0;

                        // Invert direction.
                        speedY *= -1;
                    }
                    else if (positionY > TheCanvas.ActualHeight - rectangle.Height)
                    {
                        positionY = TheCanvas.ActualHeight - rectangle.Height;

                        // Invert direction.
                        speedY *= -1;
                    }

                    rectangle.SetValue(Canvas.LeftProperty, positionX);
                    rectangle.SetValue(Canvas.TopProperty, positionY);
                }
            }
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            // Put rectangle in the middle of the screen.
            positionX = TheCanvas.ActualWidth / 2;
            positionY = TheCanvas.ActualHeight / 2;

            rectangle = new Rectangle();
            rectangle.Fill = new SolidColorBrush(Colors.White);
            rectangle.Width = 15;
            rectangle.Height = 15;
            TheCanvas.Children.Add(rectangle);
        }

        private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.V)
            {
                var ctrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if (ctrlState != CoreVirtualKeyStates.None)
                {
                    Frame.Navigate(typeof(ItemsPage));
                    e.Handled = true;
                }
            }
        }
    }
}
