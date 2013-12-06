using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.Linq;
using System.IO;
//using System.Speech.Recognition;
using System.Text;
//using System.Speech.AudioFormat;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;

namespace RobotControl
{

    public partial class MainWindow : Window
    {
        //Instantiate the Kinect runtime. Required to initialize the device.
        //IMPORTANT NOTE: You can pass the device ID here, in case more than one Kinect device is connected.
        KinectSensor sensor = KinectSensor.KinectSensors[0];
        byte[] pixelData;
        Skeleton[] skeletons;
        bool start = false;
       // private SpeechRecognitionEngine spRecEng;
        private SpeechRecognitionEngine speechRecognizer;

        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        
        public MainWindow()
        {
            InitializeComponent();

            //Runtime initialization is handled when the window is opened. When the window
            //is closed, the runtime MUST be unitialized.
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.Unloaded += new RoutedEventHandler(MainWindow_Unloaded);

            string uri = @"http://10.201.77.237:8080/";
            this.browser.Navigate(new Uri(uri, UriKind.Absolute));
            
            sensor.ColorStream.Enable();
            sensor.SkeletonStream.Enable();
            sensor.Start();
            sensor.AudioSource.Start();
            speechRecognizer = CreateSpeechRecognizer();
            sensor.AudioSource.Start();
            var audioSource = sensor.AudioSource;
            audioSource.BeamAngleMode = BeamAngleMode.Adaptive;
            audioSource.Start();
            var kinectStream = audioSource.Start();
            speechRecognizer.SetInputToAudioStream(
                kinectStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            //make sure the recognizer does not stop after completing     
            speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
            //reduce background and ambient noise for better accuracy
            sensor.AudioSource.EchoCancellationMode = EchoCancellationMode.None;
            sensor.AudioSource.AutomaticGainControlEnabled = false;
        
            
           
        }

       

        private SpeechRecognitionEngine CreateSpeechRecognizer()
        {
            //set recognizer info
            RecognizerInfo ri = GetKinectRecognizer();
            //create instance of SRE
            SpeechRecognitionEngine sre;
            
            sre = new SpeechRecognitionEngine(ri.Id);
            //Now we need to add the words we want our program to recognise
            var grammar = new Choices();
            grammar.Add("begin");
            grammar.Add("stop");

            //set culture - language, country/region
            var gb = new GrammarBuilder { Culture = ri.Culture };
            gb.Append(grammar);

            //set up the grammar builder
            var g = new Grammar(gb);
            sre.LoadGrammar(g);
            //Set events for recognizing, hypothesising and rejecting speech
            sre.SpeechRecognized += SreSpeechRecognized;
            sre.SpeechHypothesized += SreSpeechHypothesized;
            sre.SpeechRecognitionRejected += SreSpeechRecognitionRejected;
            

            return sre;
            
        }

        //if speech is rejected
        private void RejectSpeech(RecognitionResult result)
        {
            //Text1.Text = "Speech isn't recognized";
        }

        private void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            RejectSpeech(e.Result);
            start = false;
        }

        //hypothesized result
        private void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            //Text2.Text = "Hypothesized: " + e.Result.Text + " " + e.Result.Confidence;
        }

        //Speech is recognised
        private void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //Very important! - change this value to adjust accuracy - the higher the value
            //the more accurate it will have to be, lower it if it is not recognizing you
            if (e.Result.Confidence < .4)
            {
                RejectSpeech(e.Result);
            }
            //and finally, here we set what we want to happen when 
            //the SRE recognizes a word
            switch (e.Result.Text.ToUpperInvariant())
            {
                case "BEGIN":
                    //Text1.Text = "start";
                    start = true;

                    break;
                case "STOP":
                    //Text1.Text = "stop";
                    start = false;
                    break;
                default:
                    break;
            }
        }


      


        void runtime_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            bool receivedData = false;

            using (SkeletonFrame SFrame = e.OpenSkeletonFrame())
            {
                if (SFrame == null)
                {
                    // The image processing took too long. More than 2 frames behind.
                }
                else
                {
                    skeletons = new Skeleton[SFrame.SkeletonArrayLength];
                    SFrame.CopySkeletonDataTo(skeletons);
                    receivedData = true;
                }
            }

            if (receivedData)
            {

                Skeleton currentSkeleton = (from s in skeletons
                                            where s.TrackingState == SkeletonTrackingState.Tracked
                                            select s).FirstOrDefault();

                if (currentSkeleton != null)


                {
                        
                    
                    //SetEllipsePosition(head, currentSkeleton.Joints[JointType.Head]);
                    SetEllipsePosition(leftHand, currentSkeleton.Joints[JointType.HandLeft], 1);
                    SetEllipsePosition(rightHand, currentSkeleton.Joints[JointType.HandRight], 2);

                   SetSceletonVariables(currentSkeleton);
                   if (shCenterZ > 2500 && shCenterZ < 3000)
                   {
                       
                       InfoBox.Text = "You position is fine";
                       PrintSkeletonVariables();
                       //if (start)
                       //{ 
                           CalculateRobotDrive();
                           CalculateRobotCamera();
                       //}
                       //else
                       //{
                       //    SendDataToRobot("robot", 0);
                       //    SendDataToRobot("camera", 0);
                       //}
                   }
                   else
                   {
                       SendDataToRobot("robot",0);
                       SendDataToRobot("camera", 0);
                       if (shCenterZ < 2500)
                           InfoBox.Text = "Move a bit backward";
                       if (shCenterZ > 3000)
                       { 
                           InfoBox.Text = "Move a bit forward";
                       }
                   }
                   
                    


                }

               

            }
        }


        //This method is used to position the ellipses on the canvas
        //according to correct movements of the tracked joints.

        //IMPORTANT NOTE: Code for vector scaling was imported from the Coding4Fun Kinect Toolkit
        //available here: http://c4fkinect.codeplex.com/
        //I only used this part to avoid adding an extra reference.
        private void SetEllipsePosition(Ellipse ellipse, Joint joint, int hand)
        {
            Microsoft.Kinect.SkeletonPoint vector = new Microsoft.Kinect.SkeletonPoint();
            if (hand == 2)
                hand = 80;
            else hand = 0;

            vector.X = ScaleVector(640, joint.Position.X);
            vector.Y = ScaleVector(480, -joint.Position.Y);
            vector.Z = joint.Position.Z;

            Joint updatedJoint = new Joint();
            updatedJoint = joint;
            updatedJoint.TrackingState = JointTrackingState.Tracked;
            updatedJoint.Position = vector;

            Canvas.SetLeft(ellipse, updatedJoint.Position.X-hand);
            Canvas.SetTop(ellipse, updatedJoint.Position.Y);
        }

        //Skeleton center
        int shCenterX;
        int shCenterY;
        int shCenterZ;
        //Left hand positions
        int lHandX;
        int lHandZ;
        //Left hand relative distance
        int lDifferenceX;
        int lDifferenceZ;
        //Right hand position
        int rHandX;
        int rHandY;
        //Right hand relative position
        int rDifferenceX;
        int rDifferenceY;

        int multiplier = 1000; //precision multiplier

        

        private void SetSceletonVariables(Skeleton currentSkeleton)
        {    
            shCenterX = Convert.ToInt32(currentSkeleton.Joints[JointType.ShoulderCenter].Position.X * multiplier);
            shCenterY = Convert.ToInt32(currentSkeleton.Joints[JointType.ShoulderCenter].Position.Y * multiplier);
            shCenterZ = Convert.ToInt32(currentSkeleton.Joints[JointType.ShoulderCenter].Position.Z * multiplier);

            lHandX = Convert.ToInt32(currentSkeleton.Joints[JointType.HandLeft].Position.X * multiplier);
            lHandZ = Convert.ToInt32(currentSkeleton.Joints[JointType.HandLeft].Position.Z * multiplier);
            lDifferenceX = shCenterX - lHandX;
            lDifferenceZ = shCenterZ - lHandZ;

            rHandX = Convert.ToInt32(currentSkeleton.Joints[JointType.HandRight].Position.X * multiplier);
            rHandY = Convert.ToInt32(currentSkeleton.Joints[JointType.HandRight].Position.Y * multiplier);

            rDifferenceX = shCenterX - rHandX;
            rDifferenceY = shCenterZ - rHandY;
        }

        private void PrintSkeletonVariables()
        {
            LeftHandX.Text = lHandX.ToString();
            LeftHandZ.Text = lHandZ.ToString();

            RightHandX.Text = rHandX.ToString();
            RightHandY.Text = rHandY.ToString();

            CenterX.Text = shCenterX.ToString();
            CenterY.Text = shCenterY.ToString();
            CenterZ.Text = shCenterZ.ToString();

            LeftHandXRel.Text = lDifferenceX.ToString();
            LeftHandZRel.Text = lDifferenceZ.ToString();

            RightHandXRel.Text = rDifferenceX.ToString();
            RightHandYRel.Text = rDifferenceY.ToString();

            StartText.Text = start.ToString();
        }

        double lminDistanceZ = 140;
        double lmaxDistanceZ = 300;
        double lminDistanceX = 170;
        double lmaxDistanceX = 300;

        
        private bool inDefaultRange (int value, char coordSystem, string hand)
    {
        bool trigger = false;
        if (hand == "left")
        {
            switch (coordSystem)
            {
                case 'X':
                    if (Math.Abs(value) >= lminDistanceX && Math.Abs(value) <= lmaxDistanceX)
                        trigger = true;
                    break;
                case 'Z':
                    if (Math.Abs(value) >= lminDistanceZ && Math.Abs(value) <= lmaxDistanceZ)
                        trigger = true;
                    break;
            }
        }
        else
        {
            switch (coordSystem)
            {
                case 'X':
                    if (value >= rminDistanceX && value <= rmaxDistanceX)
                        trigger = true;
                    break;
                case 'Y':
                    if (value >= rminDistanceY && value <= rmaxDistanceY)
                        trigger = true;
                    break;
            }
        }
        return trigger;
    }

        int prevPosX;
        int prevPosZ;

        private void CalculateRobotDrive()
        {
            bool defRangeX;
            bool defRangeZ;
            int send_value = 0;

            defRangeX = inDefaultRange(lDifferenceX, 'X', "left");
            defRangeZ = inDefaultRange(lDifferenceZ, 'Z', "left");

            if (defRangeX==true && defRangeZ==true)
            {
                prevPosX = lDifferenceX;
                prevPosZ = lDifferenceZ;
                SendDataToRobot("robot", 0);
            }

            else
            {
                if (defRangeX == true && defRangeZ == false)
                {
                    if (lDifferenceZ < lminDistanceZ)
                        send_value = 1;
                    else if (lDifferenceZ > lmaxDistanceZ)
                        send_value = 2;
                   
                }
                else if (defRangeZ == true && defRangeX == false)
                {
                    if (lDifferenceX > lmaxDistanceX)
                        send_value = 3;
                    else if (lDifferenceX < lminDistanceX)
                        send_value = 4;
                    
                        
                }

                else if (defRangeX == false && defRangeZ == false)
                {
                    int relPosX = Math.Abs(lDifferenceX - prevPosX);
                    int relPosZ = Math.Abs(lDifferenceZ - prevPosZ);
                    //Text3.Text = "X:  " + relPosX.ToString();
                    //Text4.Text = "Z   " + relPosZ.ToString();

                    if (relPosX > relPosZ)
                    {

                        if (lDifferenceX > lmaxDistanceX)
                            send_value = 3;
                        else if (lDifferenceX < lminDistanceX)
                            send_value = 4;
                    }
                    else
                    {
                        if (lDifferenceZ < lminDistanceZ)
                            send_value = 1;
                        else if (lDifferenceZ > lmaxDistanceZ)
                            send_value = 2;
                    }
                }
                SendDataToRobot("robot", send_value);
            }

               
           
          
                //Text2.Text = inDefaultRange(lDifferenceZ, 'Z').ToString();
                //Text3.Text = inDefaultRange(lDifferenceX, 'X').ToString();
                //Text4.Text = lDifferenceX.ToString();
            //Text2.Text = "170  " + lDifferenceX.ToString() + "  300";




          
           
        }

        double rminDistanceY = 1700;
        double rmaxDistanceY = 2300;
        double rminDistanceX = -300;
        double rmaxDistanceX = -200;


        private void CalculateRobotCamera()
        {
            int value = 0;
            if (rDifferenceX < rminDistanceX)
                value = 2;
            else if (rDifferenceX > rmaxDistanceX)
                value = 1;
             if (rDifferenceY < rminDistanceY)
                value = 3;
            else if (rDifferenceY > rmaxDistanceY)
                value = 4;
            SendDataToRobot("camera", value);

        }
        private void SendDataToRobot(string str, int value)
        {
            StreamWriter sw = new StreamWriter(@"D:\" + str + ".txt");
            sw.Write(value);
            sw.Close();
        }


        private float ScaleVector(int length, float position)
        {
            float value = (((((float)length) / 1f) / 2f) * position) + (length / 2);
            if (value > length)
            {
                return (float)length;
            }
            if (value < 0f)
            {
                return 0f;
            }
            return value;
        }

        void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            sensor.Stop();
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            sensor.SkeletonFrameReady += runtime_SkeletonFrameReady;
            sensor.ColorFrameReady += runtime_VideoFrameReady;
            sensor.Start();
         
        



            sensor.ColorStream.Enable();
            sensor.SkeletonStream.Enable();


        }   

       
        
        void runtime_VideoFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            bool receivedData = false;

            using (ColorImageFrame CFrame = e.OpenColorImageFrame())
            {
                if (CFrame == null)
                {
                    // The image processing took too long. More than 2 frames behind.
                }
                else
                {
                    pixelData = new byte[CFrame.PixelDataLength];
                    CFrame.CopyPixelDataTo(pixelData);
                    receivedData = true;
                }
            }

            if (receivedData)
            {
                BitmapSource source = BitmapSource.Create(640, 480, 96, 96,
                        PixelFormats.Bgr32, null, pixelData, 640 * 4);

                videoImage.Source = source;
            }
        }
    }




}
