﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

//Extra
using System.Threading;

//EMGU
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.Util;

//DiresctShow
using DirectShowLib;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;

namespace StereoImaging
{
    public partial class Form1 : Form
    {
        /// CAMERA CAPTURE EXAMPLE
        private Mat _frame2;
        private Mat _frame1;
        private Mat _grayFrame1;
        private Mat _grayFrame2;
        private bool _find1;
        private bool _find2;

        private Size _patternSize;  //size of chess board to be detected
        VectorOfPointF _corners1 = new VectorOfPointF(); //corners found from chessboard
        VectorOfPointF _corners2 = new VectorOfPointF(); //corners found from chessboard
        //static Mat[] _frameArrayBuffer;
        int _frameBufferSavepoint;
        //private float _squareSize;
        const int _chessboardWidth = 8;//6;//9 //width of chessboard no. squares in width - 1
        const int _chessboardHeight = 6;//3;//6 // heght of chess board no. squares in heigth - 1




        /// STEREO IMAGING EXAMPLE
        #region Devices
        VideoCapture _Capture1;
        VideoCapture _Capture2;
        #endregion

        #region Image Processing

        //Frames
        //Image<Bgr, Byte> frame_S1;
        //Image<Gray, Byte> Gray_frame_S1;
        //Image<Bgr, Byte> frame_S2;
        //Image<Gray, Byte> Gray_frame_S2;

        //Chessboard detection

        //Size patternSize = new Size(width, height); //size of chess board to be detected
        Bgr[] line_colour_array = new Bgr[_chessboardWidth * _chessboardHeight]; // just for displaying coloured lines of detected chessboard
        PointF[] corners_Left;
        PointF[] corners_Right;
        bool start_Flag = true; //start straight away

        //buffers
        static int buffer_length = 100; //define the aquasition length of the buffer 

        //int buffer_savepoint = 0; //tracks the filled partition of the buffer
        MCvPoint3D32f[][] corners_object_Points = new MCvPoint3D32f[buffer_length][]; //stores the calculated size for the chessboard
        PointF[][] corners_points_Left = new PointF[buffer_length][];//stores the calculated points from chessboard detection Camera 1
        PointF[][] corners_points_Right = new PointF[buffer_length][];//stores the calculated points from chessboard detection Camera 2

        //Calibration parmeters
        IntrinsicCameraParameters IntrinsicCam1 = new IntrinsicCameraParameters(); //Camera 1
        IntrinsicCameraParameters IntrinsicCam2 = new IntrinsicCameraParameters(); //Camera 2
        ExtrinsicCameraParameters EX_Param; //Output of Extrinsics for Camera 1 & 2
        Matrix<double> fundamental; //fundemental output matrix for StereoCalibrate
        Matrix<double> essential; //essential output matrix for StereoCalibrate
        Rectangle Rec1 = new Rectangle(); //Rectangle Calibrated in camera 1
        Rectangle Rec2 = new Rectangle(); //Rectangle Caliubrated in camera 2
        Matrix<double> Q = new Matrix<double>(4, 4); //This is what were interested in the disparity-to-depth mapping matrix
        Matrix<double> R1 = new Matrix<double>(3, 3); //rectification transforms (rotation matrices) for Camera 1.
        Matrix<double> R2 = new Matrix<double>(3, 3); //rectification transforms (rotation matrices) for Camera 1.
        Matrix<double> P1 = new Matrix<double>(3, 4); //projection matrices in the new (rectified) coordinate systems for Camera 1.
        Matrix<double> P2 = new Matrix<double>(3, 4); //projection matrices in the new (rectified) coordinate systems for Camera 2.
        private MCvPoint3D32f[] _points; //Computer3DPointsFromStereoPair
        #endregion

        #region Current mode variables
        public enum Mode
        {
            Caluculating_Stereo_Intrinsics,
            Calibrated,
            SavingFrames
        }
        Mode currentMode = Mode.SavingFrames;
        #endregion
        public Form1()
        {
            // Forms Initialization
            InitializeComponent();

            VideoCapture();

            // My Desparity Test
            //Test_Computer3DPointsFromStereoPair();

            //while(true)
            //TestVideoCapture();
            //TestProcessFrame();


        }





        public void VideoCapture()
        {
            //set up chessboard drawing array
            Random R = new Random();
            for (int i = 0; i < line_colour_array.Length; i++)
                line_colour_array[i] = new Bgr(R.Next(0, 255), R.Next(0, 255), R.Next(0, 255));


            //Set up the capture method 

            //-> Find systems cameras with DirectShow.Net dll
            //thanks to carles lloret
            DsDevice[] _SystemCamereas = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            Video_Device[] WebCams = new Video_Device[_SystemCamereas.Length];
            for (int i = 0; i < _SystemCamereas.Length; i++) WebCams[i] = new Video_Device(i, _SystemCamereas[i].Name, _SystemCamereas[i].ClassID);

            //check to see what video inputs we have available
            if (WebCams.Length < 2)
            {
                if (WebCams.Length == 0) throw new InvalidOperationException("A camera device was not detected");
                MessageBox.Show("Only 1 camera detected. Stero Imaging can not be emulated");
            }
            else if (WebCams.Length >= 2)
            {
                //if (WebCams.Length > 2) MessageBox.Show("More than 2 cameras detected. Stero Imaging will be performed using " + WebCams[0].Device_Name + " and " + WebCams[1].Device_Name);
                //_Capture1 = new VideoCapture(WebCams[0].Device_ID);
                //_Capture2 = new VideoCapture(WebCams[1].Device_ID);
                _Capture1 = new VideoCapture(2);
                _Capture2 = new VideoCapture(1);
                //We will only use 1 frame ready event this is not really safe but it fits the purpose
                //_Capture1.ImageGrabbed += TestProcessFrame;
                _Capture1.ImageGrabbed += ProcessFrame;
                _Capture2.Start(); //We make sure we start Capture device 2 first
                _Capture1.Start();
            }

            //////////// CAMERA CAPTURE EXAMPLE
            _frame1 = new Mat();
            _frame2 = new Mat();
            _grayFrame1 = new Mat();
            _grayFrame2 = new Mat();
            //_frameArrayBuffer = new Mat[(int)10]; //???????????????????????????????????????????
            _frameBufferSavepoint = 0;


            //_squareSize = (float) 4.72; 
            _patternSize = new Size(_chessboardWidth, _chessboardHeight); //size of chess board to be detected

        }




        /// <summary>
        /// Is called to process frame from camera
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void ProcessFrame(object sender, EventArgs arg)
        {

            /////////////////////// CAPTURE /////////////////////////////////////////////////////////////////////


            //// THIS IS THE EMGU 3.4 CAMERA CAPTURE EXAMPLE
            /// /// ////////////////////////////////////////////////////////////////////////////////////////////////
            if (_Capture1 != null && _Capture1.Ptr != IntPtr.Zero)
            {
                _Capture1.Retrieve(_frame1, 0);
                CvInvoke.CvtColor(_frame1, _grayFrame1, ColorConversion.Bgr2Gray);
            }
            if (_Capture2 != null && _Capture2.Ptr != IntPtr.Zero)
            {
                _Capture2.Retrieve(_frame2, 0);
                CvInvoke.CvtColor(_frame2, _grayFrame2, ColorConversion.Bgr2Gray);
            }


            //// THIS IS ALL THE STEREO IMAGING EXAMPLE 
            /// ////////////////////////////////////////////////////////////////////////////////////////////////
            #region Frame Aquasition
            //Aquire the frames or calculate two frames from one camera
            ///////////frame_S1 = _Capture1.RetrieveBgrFrame(); // This no longer exists.
            ///////////Gray_frame_S1 = frame_S1.Convert<Gray,Byte>();
            ///////////frame_S2 = _Capture2.RetrieveBgrFrame();
            ///////////Gray_frame_S2 = frame_S2.Convert<Gray,Byte>();
            #endregion





            /////////////////////// CHESS BOARD CALIBRATION /////////////////////////////////////////////////////////////////////
            //////// CAMERA CALIBRATION EXAMPLE ///////////////////////////////////////////////////////
            #region Saving Chessboard Corners in Buffer
            if (currentMode == Mode.SavingFrames)
            {

                _find1 = CvInvoke.FindChessboardCorners(_frame1,
                                                        _patternSize,   // height, width of the checkerboard squares
                                                        _corners1,      // output the chessboard corners (VectorOfPointF)
                                                        CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.Default);
                _find2 = CvInvoke.FindChessboardCorners(_frame2,
                                                        _patternSize,
                                                        _corners2,      // output the chessboard corners (VectorOfPointF)
                                                        CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.Default);
                //we use this loop so we can show a colour image rather than a gray:
                if (_find1 && _find2) //chess board found in one of the frames?
                {

                    //_frame1.Bitmap.Save("~TestImage1.png");
                    //_frame2.Bitmap.Save("~TestImage2.png");

                    //make mesurments more accurate by using FindCornerSubPixel
                    CvInvoke.CornerSubPix(_grayFrame1, _corners1, new Size(11, 11), new Size(-1, -1), new MCvTermCriteria(30, 0.1));
                    CvInvoke.CornerSubPix(_grayFrame2, _corners2, new Size(11, 11), new Size(-1, -1), new MCvTermCriteria(30, 0.1));

                    if (start_Flag) // start right away.
                    {


                        ////////////////////////////////////////// FROM STEREO CALIBRATION /// HOPE!
                        corners_Left = _corners1.ToArray();
                        corners_Right = _corners2.ToArray();
                        corners_points_Left[_frameBufferSavepoint] = corners_Left;
                        corners_points_Right[_frameBufferSavepoint] = corners_Right;


                        //save the calculated points into an array
                        //_frameArrayBuffer[_frameBufferSavepoint] = _grayFrame1; //store the image
                        _frameBufferSavepoint++; //increase buffer positon

                        //check the state of buffer
                        if (_frameBufferSavepoint == buffer_length)
                            currentMode = Mode.Caluculating_Stereo_Intrinsics; //buffer full

                        //Show state of Buffer
                        //UpdateTitle("Form1: Buffer " + _frameBufferSavepoint.ToString() + " of " + buffer_length.ToString());
                    }


                    //draw the results
                    CvInvoke.DrawChessboardCorners(_frame1, _patternSize, _corners1, _find1);
                    CvInvoke.DrawChessboardCorners(_frame2, _patternSize, _corners2, _find2);
                    string msg = string.Format("{0}/{1}", _frameBufferSavepoint + 1, buffer_length);

                    int baseLine = 0;
                    var textOrigin = new Point(_frame1.Cols - 2 * 120 - 10, _frame1.Rows - 2 * baseLine - 10);
                    CvInvoke.PutText(_frame1, msg, textOrigin, FontFace.HersheyPlain, 3, new MCvScalar(0, 0, 255), 2);


                }
                _corners1 = new VectorOfPointF();
                _find1 = false;
                _corners2 = new VectorOfPointF();
                _find2 = false;








                ////////////// STEREO CAMERA EXAMPLE /////////////////////////////////////////////////////////////////////
                //Find the chessboard in bothe images
                /////////////corners_Left = CameraCalibration.FindChessboardCorners(Gray_frame_S1, patternSize, Emgu.CV.CvEnum.CALIB_CB_TYPE.ADAPTIVE_THRESH);
                /////////////corners_Right = CameraCalibration.FindChessboardCorners(Gray_frame_S2, patternSize, Emgu.CV.CvEnum.CALIB_CB_TYPE.ADAPTIVE_THRESH);

                //we use this loop so we can show a colour image rather than a gray: //CameraCalibration.DrawChessboardCorners(Gray_Frame, patternSize, corners);
                //we we only do this is the chessboard is present in both images
                /*
                if (corners_Left != null && corners_Right != null) //chess board found in one of the frames?
                {
                    //make mesurments more accurate by using FindCornerSubPixel
                    Gray_frame_S1.FindCornerSubPix(new PointF[1][] { corners_Left }, new Size(11, 11), new Size(-1, -1), new MCvTermCriteria(30, 0.01));
                    Gray_frame_S2.FindCornerSubPix(new PointF[1][] { corners_Right }, new Size(11, 11), new Size(-1, -1), new MCvTermCriteria(30, 0.01));

                    //if go button has been pressed start aquiring frames else we will just display the points
                    if (start_Flag)
                    {
                        //save the calculated points into an array
                        corners_points_Left[buffer_savepoint] = corners_Left;
                        corners_points_Right[buffer_savepoint] = corners_Right;
                        buffer_savepoint++;//increase buffer positon

                        //check the state of buffer
                        if (buffer_savepoint == buffer_length) currentMode = Mode.Caluculating_Stereo_Intrinsics; //buffer full

                        //Show state of Buffer
                        UpdateTitle("Form1: Buffer " + buffer_savepoint.ToString() + " of " + buffer_length.ToString());
                    }

                    //draw the results
                    frame_S1.Draw(new CircleF(corners_Left[0], 3), new Bgr(Color.Yellow), 1);
                    frame_S2.Draw(new CircleF(corners_Right[0], 3), new Bgr(Color.Yellow), 1);
                    for(int i = 1; i<corners_Left.Length; i++)
                    {
                        //left
                        frame_S1.Draw(new LineSegment2DF(corners_Left[i - 1], corners_Left[i]), line_colour_array[i], 2);
                        frame_S1.Draw(new CircleF(corners_Left[i], 3), new Bgr(Color.Yellow), 1);
                        //right
                        frame_S2.Draw(new LineSegment2DF(corners_Right[i - 1], corners_Right[i]), line_colour_array[i], 2);
                        frame_S2.Draw(new CircleF(corners_Right[i], 3), new Bgr(Color.Yellow), 1);
                    }
                    //calibrate the delay bassed on size of buffer
                    //if buffer small you want a big delay if big small delay
                    Thread.Sleep(100);//allow the user to move the board to a different position
                }
                corners_Left = null;
                corners_Right = null;
                */



                ////////////// STEREO CAMERA EXAMPLE /////////////////////////////////////////////////////////////////////
                ////draw the results
                //frame_S1.Draw(new CircleF(corners_Left[0], 3), new Bgr(Color.Yellow), 1);
                //frame_S2.Draw(new CircleF(corners_Right[0], 3), new Bgr(Color.Yellow), 1);
                //for (int i = 1; i < corners_Left.Length; i++)
                //{
                //    //left
                //    frame_S1.Draw(new LineSegment2DF(corners_Left[i - 1], corners_Left[i]), line_colour_array[i], 2);
                //    frame_S1.Draw(new CircleF(corners_Left[i], 3), new Bgr(Color.Yellow), 1);
                //    //right
                //    frame_S2.Draw(new LineSegment2DF(corners_Right[i - 1], corners_Right[i]), line_colour_array[i], 2);
                //    frame_S2.Draw(new CircleF(corners_Right[i], 3), new Bgr(Color.Yellow), 1);
                //}
                ////calibrate the delay bassed on size of buffer
                ////if buffer small you want a big delay if big small delay
                //Thread.Sleep(100);//allow the user to move the board to a different position
            }
            #endregion

            // STEREO INTRINSICS - FINDING THE CAMERA RELATIONSHIP ///////////////////////////////////////
            #region Calculating Stereo Cameras Relationship
            if (currentMode == Mode.Caluculating_Stereo_Intrinsics)
            {

                /////////////////// i don't understand this function.
                //fill the MCvPoint3D32f with correct mesurments
                for (int k = 0; k < _frameBufferSavepoint; k++)
                {
                    //Fill our objects list with the real world measurments for the intrinsic calculations
                    List<MCvPoint3D32f> object_list = new List<MCvPoint3D32f>();
                    for (int i = 0; i < _chessboardHeight; i++)
                    {
                        for (int j = 0; j < _chessboardWidth; j++)
                        {
                            object_list.Add(new MCvPoint3D32f(j * 20.0F, i * 20.0F, 0.0F));
                        }
                    }
                    corners_object_Points[k] = object_list.ToArray();
                }
                //If Emgu.CV.CvEnum.CALIB_TYPE == CV_CALIB_USE_INTRINSIC_GUESS and/or CV_CALIB_FIX_ASPECT_RATIO are specified, some or all of fx, fy, cx, cy must be initialized before calling the function
                //if you use FIX_ASPECT_RATIO and FIX_FOCAL_LEGNTH options, these values needs to be set in the intrinsic parameters before the CalibrateCamera function is called. Otherwise 0 values are used as default.


                //////////////////////////////////// STEREO CAMERA EXAMPLE ///////////////////////////////////////
                try
                {
                    CameraCalibration.StereoCalibrate(corners_object_Points,
                                                      corners_points_Left,
                                                      corners_points_Right,
                                                      IntrinsicCam1,
                                                      IntrinsicCam2,
                                                      _frame1.Size,
                                                      CalibType.Default,
                                                      //Emgu.CV.CvEnum.CALIB_TYPE.DEFAULT, 
                                                      new MCvTermCriteria(0.1e5),
                                                      out EX_Param, out fundamental, out essential);
                }
                catch (Exception ex)
                {

                }

                // My Attempts 
                //CameraCalibration.StereoCalibrate(corners_object_Points, corners_points_Left, corners_points_Right, IntrinsicCam1, IntrinsicCam2, _frame1.Size,
                //                                                CalibType.Default,
                //                                                //Emgu.CV.CvEnum.CALIB_TYPE.DEFAULT, 
                //                                                new MCvTermCriteria(0.1e5),
                //                                                out EX_Param, out fundamental, out essential);


                // My ATTEMPTS /////////////////////////////////////////
                //CvInvoke.StereoCalibrate(corners_object_Points,
                //                        _corners1,
                //                        _corners2,
                //                         //corners_points_Left, 
                //                         //corners_points_Right, 
                //                         IntrinsicCam1,
                //                         //IntrinsicCam2, 
                //                         _frame1.Size,
                //                         CalibType.Default,
                //                         new MCvTermCriteria(0.1e5),
                //                         out EX_Param,
                //                         out fundamental,
                //                         out essential);

                //try
                //{
                //    var extrinsicParams = new ExtrinsicCameraParameters(); // output
                //    var essentialMatrix = new Matrix<double>(3, 3);
                //    var foundamentalMatrix = new Matrix<double>(3, 3);
                //    VectorOfVectorOfPoint3D32F objectPointVec = new VectorOfVectorOfPoint3D32F(corners_object_Points);
                //    VectorOfVectorOfPointF imagePoints1Vec = new VectorOfVectorOfPointF(corners_points_Left);
                //    VectorOfVectorOfPointF imagePoints2Vec = new VectorOfVectorOfPointF(corners_points_Right);
                //    CvInvoke.StereoCalibrate(
                //       objectPointVec,
                //       imagePoints1Vec,
                //       imagePoints2Vec,
                //       // Image: (intrinsic/internal camera parameters)
                //       IntrinsicCam1.IntrinsicMatrix,
                //       IntrinsicCam1.DistortionCoeffs,
                //       IntrinsicCam2.IntrinsicMatrix,
                //       IntrinsicCam2.DistortionCoeffs,
                //       _frame1.Size,                        // imageSize,
                //                                            // World: (extrinsic/external camera parameters)
                //       extrinsicParams.RotationVector,      // Output
                //       extrinsicParams.TranslationVector,   // Output
                //       essentialMatrix,
                //       foundamentalMatrix,
                //       CalibType.Default, //flags,
                //       new MCvTermCriteria(0.1e5)); // termCrit);
                //}
                //catch (Exception ex)
                //{

                //}




                ///// GITHUB EXAMPLE ////////////////////////////////////
                // ref: https://github.com/neutmute/emgucv/blob/master/Emgu.CV/CameraCalibration/CameraCalibration.cs
                // ref: http://homepages.inf.ed.ac.uk/rbf/CVonline/LOCAL_COPIES/EPSRC_SSAZ/node3.html

                // OPENCV Stereo Calibration
                // The function estimates transformation between two cameras making a stereo pair.
                //
                // ref: https://docs.opencv.org/2.4/modules/calib3d/doc/camera_calibration_and_3d_reconstruction.html#stereocalibrate
                // 
                // Parameters:
                // objectPoints  – Vector of vectors of the calibration pattern points.
                // imagePoints1  – Vector of vectors of the projections of the calibration pattern points, observed by the first camera.
                // imagePoints2  – Vector of vectors of the projections of the calibration pattern points, observed by the second camera.
                // cameraMatrix1 – Input / output first camera matrix:  ,  . If any of CV_CALIB_USE_INTRINSIC_GUESS, CV_CALIB_FIX_ASPECT_RATIO, CV_CALIB_FIX_INTRINSIC, or CV_CALIB_FIX_FOCAL_LENGTH are specified, some or all of the matrix components must be initialized. See the flags description for details.
                // distCoeffs1   – Input / output vector of distortion coefficients  of 4, 5, or 8 elements.The output vector length depends on the flags.
                // cameraMatrix2 – Input / output second camera matrix.The parameter is similar to cameraMatrix1.
                // distCoeffs2   – Input / output lens distortion coefficients for the second camera.The parameter is similar to distCoeffs1.
                // imageSize     – Size of the image used only to initialize intrinsic camera matrix.
                // R – Output rotation matrix between the 1st and the 2nd camera coordinate systems.
                // T – Output translation vector between the coordinate systems of the cameras.
                // E – Output essential matrix.
                // F – Output fundamental matrix.
                // term_crit – Termination criteria for the iterative optimization algorithm.
                // flags – 

                // Different flags that may be zero or a combination of the following values:
                // CV_CALIB_FIX_INTRINSIC Fix cameraMatrix ? and distCoeffs ? so that only R, T, E, and F matrices are estimated.
                // CV_CALIB_USE_INTRINSIC_GUESS Optimize some or all of the intrinsic parameters according to the specified flags.Initial values are provided by the user.
                // CV_CALIB_FIX_PRINCIPAL_POINT Fix the principal points during the optimization.
                // CV_CALIB_FIX_FOCAL_LENGTH Fix  and.
                // CV_CALIB_FIX_ASPECT_RATIO Optimize.Fix the ratio.
                // CV_CALIB_SAME_FOCAL_LENGTH Enforce  and.
                // CV_CALIB_ZERO_TANGENT_DIST Set tangential distortion coefficients for each camera to zeros and fix there.
                // CV_CALIB_FIX_K1,..., CV_CALIB_FIX_K6 Do not change the corresponding radial distortion coefficient during the optimization.If CV_CALIB_USE_INTRINSIC_GUESS is set, the coefficient from the supplied distCoeffs matrix is used.Otherwise, it is set to 0.
                // CV_CALIB_RATIONAL_MODEL Enable coefficients k4, k5, and k6.To provide the backward compatibility, this extra flag should be explicitly specified to make the calibration function use the rational model and return 8 coefficients.If the flag is not set, the function computes and returns only 5 distortion coefficients.

                //using (VectorOfVectorOfPoint3D32F objectPointVec = new VectorOfVectorOfPoint3D32F(corners_object_Points)) //stores the calculated size for the chessboard
                //using (VectorOfVectorOfPointF imagePoints1Vec = new VectorOfVectorOfPointF(_corners1))
                //using (VectorOfVectorOfPointF imagePoints2Vec = new VectorOfVectorOfPointF(_corners2))
                //{
                //    var extrinsicParams = new ExtrinsicCameraParameters(); // output
                //    var essentialMatrix = new Matrix<double>(3, 3);
                //    var foundamentalMatrix = new Matrix<double>(3, 3);

                //    CvInvoke.StereoCalibrate(
                //       objectPointVec,
                //       imagePoints1Vec,
                //       imagePoints2Vec,
                //       // Image: (intrinsic/internal camera parameters)
                //       IntrinsicCam1.IntrinsicMatrix,
                //       IntrinsicCam1.DistortionCoeffs,
                //       IntrinsicCam2.IntrinsicMatrix,
                //       IntrinsicCam2.DistortionCoeffs,
                //       _frame1.Size,                        // imageSize,
                //       // World: (extrinsic/external camera parameters)
                //       extrinsicParams.RotationVector,      // Output
                //       extrinsicParams.TranslationVector,   // Output
                //       essentialMatrix,                         
                //       foundamentalMatrix,
                //       CalibType.Default, //flags,
                //       new MCvTermCriteria(0.1e5)); // termCrit);
                //}
                ///// GITHUB EXAMPLE ////////////////////////////////////






                //MessageBox.Show("Intrinsic Calculation Complete"); //display that the mothod has been succesful
                //currentMode = Mode.Calibrated;


                ///////// STEREO RECTIFY? /////////////////////////////////////////////////
                ////Computes rectification transforms for each head of a calibrated stereo camera.
                CvInvoke.StereoRectify(IntrinsicCam1.IntrinsicMatrix,
                                       IntrinsicCam1.DistortionCoeffs,
                                       IntrinsicCam2.IntrinsicMatrix,
                                       IntrinsicCam2.DistortionCoeffs,
                                         _frame1.Size,
                                         EX_Param.RotationVector.RotationMatrix,
                                         EX_Param.TranslationVector,
                                         R1,
                                         R2,
                                         P1,
                                         P2,
                                         Q,
                                         StereoRectifyType.Default,
                                         0,
                                         _frame1.Size, ref Rec1, ref Rec2);

                //////Computes rectification transforms for each head of a calibrated stereo camera.
                //CvInvoke.cvStereoRectify(IntrinsicCam1.IntrinsicMatrix, IntrinsicCam2.IntrinsicMatrix,
                //                         IntrinsicCam1.DistortionCoeffs, IntrinsicCam2.DistortionCoeffs,
                //                         frame_S1.Size,
                //                         EX_Param.RotationVector.RotationMatrix, EX_Param.TranslationVector,
                //                         R1, R2, P1, P2, Q,
                //                         Emgu.CV.CvEnum.STEREO_RECTIFY_TYPE.DEFAULT, 0,
                //                         frame_S1.Size, ref Rec1, ref Rec2);

                //This will Show us the usable area from each camera
                MessageBox.Show("Left: " + Rec1.ToString() + " \nRight: " + Rec2.ToString());
                currentMode = Mode.Calibrated;

            }
            #endregion
            #region Caluclating disparity map after calibration
            if (currentMode == Mode.Calibrated)
            {
                Image<Gray, short> disparityMap;

                Computer3DPointsFromStereoPair(_grayFrame1, _grayFrame2, out disparityMap, out _points);
                //Computer3DPointsFromStereoPair(Gray_frame_S1, Gray_frame_S2, out disparityMap, out _points);

                // all black
                // https://stackoverflow.com/questions/39753142/disparity-map-in-emgu-cv


                //Display the disparity map
                DisparityMap.Image = disparityMap.ToBitmap();
                //DisparityMap.Image = _frame1.Bitmap;

                //Draw the accurate area
                _frame1.ToImage<Bgr, Byte>().Draw(Rec1, new Bgr(Color.Black), 10);
                _frame2.ToImage<Bgr, Byte>().Draw(Rec2, new Bgr(Color.Black), 20);
                //frame_S1.Draw(Rec1, new Bgr(Color.LimeGreen), 1);
                //frame_S2.Draw(Rec2, new Bgr(Color.LimeGreen), 1);
            }
            #endregion
            //display image
            //Video_Source1.Image = frame_S1.ToBitmap();
            //Video_Source2.Image = frame_S2.ToBitmap();

            Video_Source1.Image = _frame1.Bitmap;
            Video_Source2.Image = _frame2.Bitmap;

            if (currentMode == Mode.SavingFrames)
                Thread.Sleep(500); //allow the user to move the board to a different position


        }






        /// <summary>
        /// Given the left and right image, computer the disparity map and the 3D point cloud.
        /// </summary>
        /// <param name="left">The left image</param>
        /// <param name="right">The right image</param>
        /// <param name="disparityMap">The left disparity map</param>
        /// <param name="points">The 3D point cloud within a [-0.5, 0.5] cube</param>
        //private void Computer3DPointsFromStereoPair(Image<Gray, Byte> left, Image<Gray, Byte> right, out Image<Gray, short> disparityMap, out MCvPoint3D32f[] points)
        private void Computer3DPointsFromStereoPair(Mat left, Mat right, out Image<Gray, short> disparityMap, out MCvPoint3D32f[] points)
        {
            Size size = left.Size;

            disparityMap = new Image<Gray, short>(size);
            //thread safe calibration values


            /*This is maximum disparity minus minimum disparity. Always greater than 0. In the current implementation this parameter must be divisible by 16.*/
            int numDisparities = GetSliderValue(Num_Disparities);

            /*The minimum possible disparity value. Normally it is 0, but sometimes rectification algorithms can shift images, so this parameter needs to be adjusted accordingly*/
            int minDispatities = GetSliderValue(Min_Disparities);

            /*The matched block size. Must be an odd number >=1 . Normally, it should be somewhere in 3..11 range*/
            int SAD = GetSliderValue(SAD_Window);

            /*P1, P2 – Parameters that control disparity smoothness. The larger the values, the smoother the disparity. 
             * P1 is the penalty on the disparity change by plus or minus 1 between neighbor pixels. 
             * P2 is the penalty on the disparity change by more than 1 between neighbor pixels. 
             * The algorithm requires P2 > P1 . 
             * See stereo_match.cpp sample where some reasonably good P1 and P2 values are shown 
             * (like 8*number_of_image_channels*SADWindowSize*SADWindowSize and 32*number_of_image_channels*SADWindowSize*SADWindowSize , respectively).*/

            int P1 = 8 * 1 * SAD * SAD;//GetSliderValue(P1_Slider);
            int P2 = 32 * 1 * SAD * SAD;//GetSliderValue(P2_Slider);

            /* Maximum allowed difference (in integer pixel units) in the left-right disparity check. Set it to non-positive value to disable the check.*/
            int disp12MaxDiff = GetSliderValue(Disp12MaxDiff);

            /*Truncation value for the prefiltered image pixels. 
             * The algorithm first computes x-derivative at each pixel and clips its value by [-preFilterCap, preFilterCap] interval. 
             * The result values are passed to the Birchfield-Tomasi pixel cost function.*/
            int PreFilterCap = GetSliderValue(pre_filter_cap);

            /*The margin in percents by which the best (minimum) computed cost function value should “win” the second best value to consider the found match correct. 
             * Normally, some value within 5-15 range is good enough*/
            int UniquenessRatio = GetSliderValue(uniquenessRatio);

            /*Maximum disparity variation within each connected component. 
             * If you do speckle filtering, set it to some positive value, multiple of 16. 
             * Normally, 16 or 32 is good enough*/
            int Speckle = GetSliderValue(Speckle_Window);

            /*Maximum disparity variation within each connected component. If you do speckle filtering, set it to some positive value, multiple of 16. Normally, 16 or 32 is good enough.*/
            int SpeckleRange = GetSliderValue(specklerange);

            /*Set it to true to run full-scale 2-pass dynamic programming algorithm. It will consume O(W*H*numDisparities) bytes, 
             * which is large for 640x480 stereo and huge for HD-size pictures. By default this is usually false*/
            //Set globally for ease
            //bool fullDP = true;

            using (StereoSGBM stereoSolver = new StereoSGBM(minDispatities, numDisparities, SAD, P1, P2, disp12MaxDiff, PreFilterCap, UniquenessRatio, Speckle, SpeckleRange, 0))
            //using (StereoBM stereoSolver = new StereoBM(Emgu.CV.CvEnum.STEREO_BM_TYPE.BASIC, 0))
            {
                //stereoSolver.FindStereoCorrespondence(left, right, disparityMap);//Computes the disparity map using: 
                stereoSolver.Compute(left, right, disparityMap);
                /*GC: graph cut-based algorithm
                  BM: block matching algorithm
                  SGBM: modified H. Hirschmuller algorithm HH08*/
                points = PointCollection.ReprojectImageTo3D(disparityMap, Q); //Reprojects disparity image to 3D space.
            }
        }



        private void Call_Test_Computer3DPointsFromStereoPair(object sender, EventArgs arg)
        {
            Test_Computer3DPointsFromStereoPair();
        }


        private void Test_Computer3DPointsFromStereoPair()
        {
            MCvPoint3D32f[] _points;
            Mat _left = CvInvoke.Imread("imL.png", ImreadModes.Color);
            Mat _right = CvInvoke.Imread("imR.png", ImreadModes.Color);
            //Mat disparityMap = new Mat();
            Image<Gray, short> disparityImage;

            Mat leftGray = new Mat();
            Mat rightGray = new Mat();

            CvInvoke.CvtColor(_left, leftGray, ColorConversion.Bgr2Gray);
            CvInvoke.CvtColor(_right, rightGray, ColorConversion.Bgr2Gray);
            Mat points = new Mat();

            Computer3DPointsFromStereoPair(leftGray, rightGray, out disparityImage, out _points);

            Mat pointsArray = points.Reshape(points.NumberOfChannels, points.Rows * points.Cols);
            Mat colorArray = _left.Reshape(_left.NumberOfChannels, _left.Rows * _left.Cols);

            Mat colorArrayFloat = new Mat();
            colorArray.ConvertTo(colorArrayFloat, DepthType.Cv32F);
            //WCloud cloud = new WCloud(pointsArray, colorArray);


            //Image<Bgr, Byte> image = disparityMap.ToImage<Bgr, Byte>();

            // 2D Disparity Display
            //Mat show = new Mat();
            //disparityMap.ConvertTo(show, DepthType.Cv8U);
            //CvInvoke.Imshow("Disparity", show);
            DisparityMap.Image = disparityImage.ToBitmap();



        }

        private void TestProcessFrame(object sender, EventArgs arg)
        {
            Mat _iframe1 = CvInvoke.Imread("TestImage1.png", ImreadModes.Color);
            Mat _iframe2 = CvInvoke.Imread("TestImage2.png", ImreadModes.Color);

            Mat _outframe1 = _iframe1;
            Mat _outframe2 = _iframe2;

            _grayFrame1 = new Mat();
            _grayFrame2 = new Mat();
            CvInvoke.CvtColor(_iframe1, _grayFrame1, ColorConversion.Bgr2Gray);
            CvInvoke.CvtColor(_iframe2, _grayFrame2, ColorConversion.Bgr2Gray);

            //// SETUP /////////////////////////////////////////////////
            //set up chessboard drawing array
            Random R = new Random();
            for (int i = 0; i < line_colour_array.Length; i++)
                line_colour_array[i] = new Bgr(R.Next(0, 255), R.Next(0, 255), R.Next(0, 255));


            //_squareSize = (float) 4.72; 
            _patternSize = new Size(_chessboardWidth, _chessboardHeight); //size of chess board to be detected


            /////////////////////// CHESS BOARD CALIBRATION /////////////////////////////////////////////////////////////////////
            //////// CAMERA CALIBRATION EXAMPLE ///////////////////////////////////////////////////////
            #region Saving Chessboard Corners in Buffer
            _frameBufferSavepoint = 0;
            if (currentMode == Mode.SavingFrames)
            {
                while (_frameBufferSavepoint < buffer_length)
                {

                    _find1 = CvInvoke.FindChessboardCorners(_iframe1,
                                                            _patternSize,   // height, width of the checkerboard squares
                                                            _corners1,      // output the chessboard corners (VectorOfPointF)
                                                            CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.NormalizeImage);

                    _find2 = CvInvoke.FindChessboardCorners(_iframe2,
                                                            _patternSize,
                                                            _corners2,      // output the chessboard corners (VectorOfPointF)
                                                            CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.NormalizeImage);
                    //we use this loop so we can show a colour image rather than a gray:
                    if (_find1 && _find2) //chess board found in one of the frames?
                    {

                        _outframe1 = _iframe1;
                        _outframe2 = _iframe2;

                        //make mesurments more accurate by using FindCornerSubPixel
                        CvInvoke.CornerSubPix(_grayFrame1, _corners1, new Size(11, 11), new Size(-1, -1), new MCvTermCriteria(30, 0.1));
                        CvInvoke.CornerSubPix(_grayFrame2, _corners2, new Size(11, 11), new Size(-1, -1), new MCvTermCriteria(30, 0.1));

                        if (start_Flag) // start right away.
                        {
                            ////////////////////////////////////////// FROM STEREO CALIBRATION /// HOPE!
                            corners_Left = _corners1.ToArray();
                            corners_Right = _corners2.ToArray();
                            corners_points_Left[_frameBufferSavepoint] = corners_Left;
                            corners_points_Right[_frameBufferSavepoint] = corners_Right;

                            //save the calculated points into an array
                            //_frameArrayBuffer[_frameBufferSavepoint] = _grayFrame1; //store the image
                            _frameBufferSavepoint++; //increase buffer positon

                            //check the state of buffer
                            if (_frameBufferSavepoint == buffer_length)
                                currentMode = Mode.Caluculating_Stereo_Intrinsics; //buffer full
                        }

                        //draw the results
                        //CvInvoke.DrawChessboardCorners(_outframe1, _patternSize, _corners1, _find1);
                        //CvInvoke.DrawChessboardCorners(_outframe2, _patternSize, _corners2, _find2);
                        //string msg = string.Format("{0}/{1}", _frameBufferSavepoint + 1, buffer_length);

                        int baseLine = 0;
                        //var textOrigin = new Point(_outframe1.Cols - 2 * 120 - 10, _outframe1.Rows - 2 * baseLine - 10);
                        //CvInvoke.PutText(_outframe1, msg, textOrigin, FontFace.HersheyPlain, 3, new MCvScalar(0, 0, 255), 2);


                    }
                    _corners1 = new VectorOfPointF();
                    _find1 = false;
                    _corners2 = new VectorOfPointF();
                    _find2 = false;

                }
            }
            #endregion

            // STEREO INTRINSICS - FINDING THE CAMERA RELATIONSHIP ///////////////////////////////////////
            #region Calculating Stereo Cameras Relationship
            if (currentMode == Mode.Caluculating_Stereo_Intrinsics)
            {

                /////////////////// i don't understand this function.
                //fill the MCvPoint3D32f with correct mesurments
                for (int k = 0; k < _frameBufferSavepoint; k++)
                {
                    //Fill our objects list with the real world measurments for the intrinsic calculations
                    List<MCvPoint3D32f> object_list = new List<MCvPoint3D32f>();
                    for (int i = 0; i < _chessboardHeight; i++)
                    {
                        for (int j = 0; j < _chessboardWidth; j++)
                        {
                            object_list.Add(new MCvPoint3D32f(j * 20.0F, i * 20.0F, 0.0F));
                        }
                    }
                    corners_object_Points[k] = object_list.ToArray();
                }
                //If Emgu.CV.CvEnum.CALIB_TYPE == CV_CALIB_USE_INTRINSIC_GUESS and/or CV_CALIB_FIX_ASPECT_RATIO are specified, some or all of fx, fy, cx, cy must be initialized before calling the function
                //if you use FIX_ASPECT_RATIO and FIX_FOCAL_LEGNTH options, these values needs to be set in the intrinsic parameters before the CalibrateCamera function is called. Otherwise 0 values are used as default.


                //////////////////////////////////// STEREO CAMERA EXAMPLE ///////////////////////////////////////
                try
                {
                    CameraCalibration.StereoCalibrate(corners_object_Points,
                                                      corners_points_Left,
                                                      corners_points_Right,
                                                      IntrinsicCam1,
                                                      IntrinsicCam2,
                                                      _iframe1.Size,
                                                      CalibType.Default,
                                                      //Emgu.CV.CvEnum.CALIB_TYPE.DEFAULT, 
                                                      new MCvTermCriteria(0.1e5),
                                                      out EX_Param, out fundamental, out essential);
                }
                catch (Exception ex)
                {

                }


                ///////// STEREO RECTIFY? /////////////////////////////////////////////////
                ////Computes rectification transforms for each head of a calibrated stereo camera.
                CvInvoke.StereoRectify(IntrinsicCam1.IntrinsicMatrix,
                                       IntrinsicCam1.DistortionCoeffs,
                                       IntrinsicCam2.IntrinsicMatrix,
                                       IntrinsicCam2.DistortionCoeffs,
                                         _iframe1.Size,
                                         EX_Param.RotationVector.RotationMatrix,
                                         EX_Param.TranslationVector,
                                         R1,
                                         R2,
                                         P1,
                                         P2,
                                         Q,
                                         StereoRectifyType.Default,
                                         0,
                                         _iframe1.Size, ref Rec1, ref Rec2);


                //This will Show us the usable area from each camera
                MessageBox.Show("Left: " + Rec1.ToString() + " \nRight: " + Rec2.ToString());
                currentMode = Mode.Calibrated;

            }
            #endregion
            #region Caluclating disparity map after calibration
            if (currentMode == Mode.Calibrated)
            {
                Image<Gray, short> disparityMap;

                Computer3DPointsFromStereoPair(_grayFrame1, _grayFrame2, out disparityMap, out _points);
                //Computer3DPointsFromStereoPair(Gray_frame_S1, Gray_frame_S2, out disparityMap, out _points);

                // all black
                // https://stackoverflow.com/questions/39753142/disparity-map-in-emgu-cv


                //Display the disparity map
                DisparityMap.Image = disparityMap.ToBitmap();
                //DisparityMap.Image = _frame1.Bitmap;

                //Draw the accurate area
                //_frame1.ToImage<Bgr, Byte>().Draw(Rec1, new Bgr(Color.LimeGreen), 1);
                //_frame2.ToImage<Bgr, Byte>().Draw(Rec2, new Bgr(Color.LimeGreen), 1);
                //frame_S1.Draw(Rec1, new Bgr(Color.LimeGreen), 1);
                //frame_S2.Draw(Rec2, new Bgr(Color.LimeGreen), 1);
            }
            #endregion
            //display image
            //Video_Source1.Image = frame_S1.ToBitmap();
            //Video_Source2.Image = frame_S2.ToBitmap();

            Video_Source1.Image = _outframe1.Bitmap;
            Video_Source2.Image = _outframe2.Bitmap;

            if (currentMode == Mode.SavingFrames)
                Thread.Sleep(500); //allow the user to move the board to a different position

        }



        public void TestVideoCapture()
        {
            //Set up the capture method 

            //-> Find systems cameras with DirectShow.Net dll
            //thanks to carles lloret
            DsDevice[] _SystemCamereas = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            Video_Device[] WebCams = new Video_Device[_SystemCamereas.Length];
            for (int i = 0; i < _SystemCamereas.Length; i++) WebCams[i] = new Video_Device(i, _SystemCamereas[i].Name, _SystemCamereas[i].ClassID);

            //check to see what video inputs we have available
            if (WebCams.Length < 2)
            {
                if (WebCams.Length == 0) throw new InvalidOperationException("A camera device was not detected");
                MessageBox.Show("Only 1 camera detected. Stero Imaging can not be emulated");
            }
            else if (WebCams.Length >= 2)
            {
                //if (WebCams.Length > 2) MessageBox.Show("More than 2 cameras detected. Stero Imaging will be performed using " + WebCams[0].Device_Name + " and " + WebCams[1].Device_Name);
                //_Capture1 = new VideoCapture(WebCams[0].Device_ID);
                //_Capture2 = new VideoCapture(WebCams[1].Device_ID);
                _Capture1 = new VideoCapture(2);
                _Capture2 = new VideoCapture(1);
                //We will only use 1 frame ready event this is not really safe but it fits the purpose
                _Capture1.ImageGrabbed += TestProcessFrame;
                _Capture2.Start(); //We make sure we start Capture device 2 first
                _Capture1.Start();
            }

        }

        #region Window/Form Control
        /// <summary>
        /// Thread safe method to get a slider value from form
        /// </summary>
        /// <param name="Control"></param>
        /// <returns></returns>
        private delegate int GetSlideValueDelgate(TrackBar Control);
        private int GetSliderValue(TrackBar Control)
        {
            if (Control.InvokeRequired)
            {
                try
                {
                    return (int)Control.Invoke(new Func<int>(() => GetSliderValue(Control)));
                }
                catch(Exception ex)
                {
                    return 0;
                }
            }
            else
            {
                return Control.Value;
            }
        }

        private delegate void UpateTitleDelgate(String Text);
        private void UpdateTitle(String Text)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    // update title asynchronously
                    UpateTitleDelgate ut = new UpateTitleDelgate(UpdateTitle); 
                    //if (this.IsHandleCreated && !this.IsDisposed)
                    this.BeginInvoke(ut, new object[] { Text });
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                this.Text = Text;
            }
        }

        /// <summary>
        /// The matched block size. Must be an odd number >=1 . Normally, it should be somewhere in 3..11 range
        /// Each time the slider moves the value is checked and made odd if even
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SAD_Window_Scroll(object sender, EventArgs e)
        {
            /*The matched block size. Must be an odd number >=1 . Normally, it should be somewhere in 3..11 range*/
            //This ensures only odd numbers are allowed from slider value
            if (SAD_Window.Value % 2 == 0)
            {
                if (SAD_Window.Value == SAD_Window.Maximum) SAD_Window.Value = SAD_Window.Maximum - 2;
                else SAD_Window.Value++;
            } 
        }

        /// <summary>
        /// This is maximum disparity minus minimum disparity. Always greater than 0. In the current implementation this parameter must be divisible by 16.
        /// Each time the slider moves the value is checked and made a factor of 16
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Num_Disparities_Scroll(object sender, EventArgs e)
        {

            if (Num_Disparities.Value % 16 != 0)
            {
                //value must be divisable by 16
                if (Num_Disparities.Value >= 152) Num_Disparities.Value = 160;
                else if (Num_Disparities.Value >= 136) Num_Disparities.Value = 144;
                else if (Num_Disparities.Value >= 120) Num_Disparities.Value = 128;
                else if (Num_Disparities.Value >= 104) Num_Disparities.Value = 112;
                else if (Num_Disparities.Value >= 88) Num_Disparities.Value = 96;
                else if (Num_Disparities.Value >= 72) Num_Disparities.Value = 80;
                else if (Num_Disparities.Value >= 56) Num_Disparities.Value = 64;
                else if (Num_Disparities.Value >= 40) Num_Disparities.Value = 48;
                else if (Num_Disparities.Value >= 24) Num_Disparities.Value = 32;
                else Num_Disparities.Value = 16;
            }
        }

        /// <summary>
        /// Maximum disparity variation within each connected component. If you do speckle filtering, set it to some positive value, multiple of 16. Normally, 16 or 32 is good enough.
        /// Each time the slider moves the value is checked and made a factor of 16
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void specklerange_Scroll(object sender, EventArgs e)
        {
            if (specklerange.Value % 16 != 0)
            {
                //value must be divisable by 16
                //TODO: we can do this in a loop
                if (specklerange.Value >= 152) specklerange.Value = 160;
                else if (specklerange.Value >= 136) specklerange.Value = 144;
                else if (specklerange.Value >= 120) specklerange.Value = 128;
                else if (specklerange.Value >= 104) specklerange.Value = 112;
                else if (specklerange.Value >= 88) specklerange.Value = 96;
                else if (specklerange.Value >= 72) specklerange.Value = 80;
                else if (specklerange.Value >= 56) specklerange.Value = 64;
                else if (specklerange.Value >= 40) specklerange.Value = 48;
                else if (specklerange.Value >= 24) specklerange.Value = 32;
                else if (specklerange.Value >= 8) specklerange.Value = 16;
                else specklerange.Value = 0;
            }
        }

        /// <summary>
        /// Sets the state of fulldp in the StereoSGBM algorithm allowing full-scale 2-pass dynamic programming algorithm. 
        /// It will consume O(W*H*numDisparities) bytes, which is large for 640x480 stereo and huge for HD-size pictures. By default this is false
        /// </summary>
        bool fullDP = false;
        private void fullDP_State_Click(object sender, EventArgs e)
        {
            if (fullDP_State.Text == "True")
            {
                fullDP = false;
                fullDP_State.Text = "False";
            }
            else
            {
                fullDP = true;
                fullDP_State.Text = "True";
            }
        }

        /// <summary>
        /// Overide form closing event to release cameras
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_Capture1 != null) _Capture1.Dispose();
            if (_Capture2 != null) _Capture2.Dispose();
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            OnClosing(null);
        }
        #endregion
    }
}

/// <summary>
/// Structure to Store Information about Video Devices
/// </summary>
struct Video_Device
{
    public string Device_Name;
    public int Device_ID;
    public Guid Identifier;

    public Video_Device(int ID, string Name, Guid Identity = new Guid())
    {
        Device_ID = ID;
        Device_Name = Name;
        Identifier = Identity;
    }

    // <summary>
    /// Represent the Device as a String
    /// </summary>H:\Live Mesh\Visual Studio 2010\Projects\Examples\EMGU x64\Emgu.CV.Example\CameraCapture V2.0\Structures.cs
    /// <returns>The string representation of this color</returns>
    public override string ToString()
    {
        return String.Format("[{0} {1}:{2}]", Device_ID, Device_Name, Identifier);
    }
}