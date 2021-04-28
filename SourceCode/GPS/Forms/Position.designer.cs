﻿//Please, if you use this, share the improvements

using System;
using System.Collections.Generic;
using System.Text;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        //very first fix to setup grid etc
        public bool isFirstFixPositionSet = false, isGPSPositionInitialized = false;

        //string to record fixes for elevation maps
        public StringBuilder sbFix = new StringBuilder();

        // autosteer variables for sending serial
        public Int16 guidanceLineDistanceOff, guidanceLineSteerAngle, distanceDisplay;

        //how many fix updates per sec
        public int fixUpdateHz = 5;
        public double fixUpdateTime = 0.2;

        //for heading or Atan2 as camera
        public string headingFromSource, headingFromSourceBak;

        public vec3 pivotAxlePos = new vec3(0, 0, 0);
        public vec3 steerAxlePos = new vec3(0, 0, 0);
        public vec3 toolPos = new vec3(0, 0, 0);
        public vec3 tankPos = new vec3(0, 0, 0);
        public vec2 hitchPos = new vec2(0, 0);

        //history
        public vec2 prevFix = new vec2(0, 0);

        //headings
        public double fixHeading = 0.0, camHeading = 0.0, gpsHeading = 0.0, prevGPSHeading = 0.0;

        //storage for the cos and sin of heading
        public double cosSectionHeading = 1.0, sinSectionHeading = 0.0;

        //a distance between previous and current fix
        private double distance = 0.0;
        public double treeSpacingCounter = 0.0;
        public int treeTrigger = 0;

        //how far travelled since last section was added, section points
        double sectionTriggerDistance = 0, sectionTriggerStepDistance = 0;
        public vec2 prevSectionPos = new vec2(0, 0);

        public vec2 prevBoundaryPos = new vec2(0, 0);

        //are we still getting valid data from GPS, resets to 0 in NMEA OGI block, watchdog 
        public int recvCounter = 20;

        //Everything is so wonky at the start
        int startCounter = 0;

        //individual points for the flags in a list
        public List<CFlag> flagPts = new List<CFlag>();

        //tally counters for display
        //public double totalSquareMetersWorked = 0, totalUserSquareMeters = 0, userSquareMetersAlarm = 0;

        public double avgSpeed;//for average speed
        public int crossTrackError;

        //youturn
        public double distancePivotToTurnLine = -2222;
        public double distanceToolToTurnLine = -2222;

        //the value to fill in you turn progress bar
        public int youTurnProgressBar = 0;

        //IMU 
        public double rollCorrectionDistance = 0;
        double gyroCorrection, gyroCorrected;

        //step position - slow speed spinner killer
        private int currentStepFix = 0;
        private const int totalFixSteps = 20;
        public vecFix2Fix[] stepFixPts = new vecFix2Fix[totalFixSteps];
        public double distanceCurrentStepFix = 0, fixStepDist, minFixStepDist = 1, startSpeed = 0.5;

        private double nowHz = 0;

        public bool isRTK;

        //called by watchdog timer every 10 ms, returns true if new valid fix
        private bool ScanForNMEA()
        {
            //update the recv string so it can display at least something
            recvSentenceSettings = pn.rawBuffer;

            //parse any data from pn.rawBuffer
            pn.ParseNMEA();

            //time for a frame update with new valid nmea data
            if (pn.updatedGGA | pn.updatedOGI | pn.updatedRMC)
            {
                //Measure the frequency of the GPS updates
                swHz.Stop();
                nowHz = ((double)System.Diagnostics.Stopwatch.Frequency) / (double)swHz.ElapsedTicks;

                //simple comp filter
                if (nowHz < 20) HzTime = 0.97 * HzTime + 0.03 * nowHz;
                //HzTime = Math.Round(HzTime, 0);

                //auto set gps freq
                fixUpdateHz = (int)(HzTime + 0.5);
                fixUpdateTime = 1 / HzTime;


                swHz.Reset();
                swHz.Start();

                //reset  flags
                pn.updatedGGA = false;
                pn.updatedOGI = false;
                pn.updatedRMC = false;

                //start the watch and time till it finishes
                swFrame.Reset();
                swFrame.Start();


                //update all data for new frame
                UpdateFixPosition();
                //Update the port connection counter - is reset every time new sentence is valid and ready
                recvCounter++;


                //new position updated
                return true;
            }
            else
            {
                return false;
            }
        }

        public double rollUsed;
        public double headlandDistanceDelta = 0, boundaryDistanceDelta = 0;

        private void UpdateFixPosition()
        {
            startCounter++;

            if (!isGPSPositionInitialized)             
            { 
                InitializeFirstFewGPSPositions(); 
                return;            
            }
           
            switch (headingFromSource)
                {
                    case "Fix":
                        {

                            //calculate current heading only when moving, otherwise use last
                            if (Math.Abs(avgSpeed) > startSpeed) //set in steer settings, Stanley
                            {
                                prevFix.easting = stepFixPts[0].easting; prevFix.northing = stepFixPts[0].northing;

                                if (stepFixPts[2].isSet == 0)
                                {
                                    //this is the first position no roll or offset correction
                                    if (stepFixPts[0].isSet == 0)
                                    {
                                        stepFixPts[0].easting = pn.fix.easting;
                                        stepFixPts[0].northing = pn.fix.northing;
                                        stepFixPts[0].isSet = 1;
                                        return;
                                    }

                                    //and the second
                                    if (stepFixPts[1].isSet == 0)
                                    {
                                        for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                                        stepFixPts[0].easting = pn.fix.easting;
                                        stepFixPts[0].northing = pn.fix.northing;
                                        stepFixPts[0].isSet = 1;
                                        return;
                                    }

                                    //the critcal moment for checking initial direction/heading.
                                    for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                                    stepFixPts[0].easting = pn.fix.easting;
                                    stepFixPts[0].northing = pn.fix.northing;
                                    stepFixPts[0].isSet = 1;

                                    gpsHeading = Math.Atan2(pn.fix.easting - stepFixPts[2].easting,
                                        pn.fix.northing - stepFixPts[2].northing);

                                    if (gpsHeading < 0) gpsHeading += glm.twoPI;
                                    else if (gpsHeading > glm.twoPI) gpsHeading -= glm.twoPI;

                                    //if (lastValidHeading != 10)
                                    //{
                                    //    //what is angle between the last valid heading before stopping and one just now
                                    //    double delta = Math.Abs(Math.PI - Math.Abs(Math.Abs(lastValidHeading - gpsHeading) - Math.PI));

                                    //    //ie change in direction
                                    //    if (delta > 2.8) vehicle.isReverse = !vehicle.isReverse;
                                    //}

                                    //if (vehicle.isReverse) gpsHeading += Math.PI;

                                    if (gpsHeading < 0) gpsHeading += glm.twoPI;
                                    if (gpsHeading >= glm.twoPI) gpsHeading -= glm.twoPI;

                                    fixHeading = gpsHeading;
                                    camHeading = glm.toDegrees(gpsHeading);

                                    //now we have a heading, fix the first 3
                                    if (vehicle.antennaOffset != 0)
                                    {
                                        for (int i = 0; i < 3; i++)
                                        {
                                            stepFixPts[i].easting = (Math.Cos(-gpsHeading) * vehicle.antennaOffset) + stepFixPts[i].easting;
                                            stepFixPts[i].northing = (Math.Sin(-gpsHeading) * vehicle.antennaOffset) + stepFixPts[i].northing;
                                        }
                                    }

                                    rollUsed = 0;

                                    if ((ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR) && !ahrs.isRollFromOGI)
                                    {
                                        rollUsed = ((double)(ahrs.rollX16 - ahrs.rollZeroX16)) * 0.0625;
                                    }


                                    if (rollUsed != 0)
                                    {

                                        //change for roll to the right is positive times -1
                                        rollCorrectionDistance = Math.Sin(glm.toRadians((rollUsed))) * -vehicle.antennaHeight;

                                        // roll to left is positive  **** important!!
                                        // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                                        for (int i = 0; i < 3; i++)
                                        {
                                            stepFixPts[i].easting = (Math.Cos(-gpsHeading) * rollCorrectionDistance) + stepFixPts[i].easting;
                                            stepFixPts[i].northing = (Math.Sin(-gpsHeading) * rollCorrectionDistance) + stepFixPts[i].northing;
                                        }
                                    }

                                    //get the distance from first to 3rd point, update fix with new offset/roll
                                    stepFixPts[0].distance = glm.Distance(stepFixPts[1], stepFixPts[0]);
                                    pn.fix.easting = stepFixPts[0].easting;
                                    pn.fix.northing = stepFixPts[0].northing;
                                }

                                //third positon and all new ones are corrected based on last gpsHeading
                                else
                                {
                                    if (vehicle.antennaOffset != 0)
                                    {
                                        pn.fix.easting = (Math.Cos(-gpsHeading) * vehicle.antennaOffset) + pn.fix.easting;
                                        pn.fix.northing = (Math.Sin(-gpsHeading) * vehicle.antennaOffset) + pn.fix.northing;
                                    }

                                    //originalEasting = pn.fix.easting;
                                    rollUsed = 0;

                                    if ((ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR) && !ahrs.isRollFromOGI)
                                    {
                                        rollUsed = ((double)(ahrs.rollX16 - ahrs.rollZeroX16)) * 0.0625;
                                    }


                                    if (rollUsed != 0)
                                    {

                                        //change for roll to the right is positive times -1
                                        rollCorrectionDistance = Math.Sin(glm.toRadians((rollUsed))) * -vehicle.antennaHeight;


                                        // roll to left is positive  **** important!!
                                        // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                                        pn.fix.easting = (Math.Cos(-gpsHeading) * rollCorrectionDistance) + pn.fix.easting;
                                        pn.fix.northing = (Math.Sin(-gpsHeading) * rollCorrectionDistance) + pn.fix.northing;

                                        //fixedEasting = pn.fix.easting;
                                    }

                                    //how far since last fix
                                    distanceCurrentStepFix = glm.Distance(stepFixPts[0], pn.fix);

                                    //save current fix and distance and set as valid
                                    for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                                    stepFixPts[0].easting = pn.fix.easting;
                                    stepFixPts[0].northing = pn.fix.northing;
                                    stepFixPts[0].isSet = 1;
                                    stepFixPts[0].distance = distanceCurrentStepFix;

                                    //find back the fix to fix distance, then heading
                                    double dist = 0;
                                    for (int i = 1; i < totalFixSteps; i++)
                                    {
                                        if (stepFixPts[i].isSet == 0)
                                        {
                                            currentStepFix = i - 1;
                                            break;
                                        }
                                        dist += stepFixPts[i - 1].distance;
                                        currentStepFix = i;
                                        if (dist > minFixStepDist)
                                            break;
                                    }

                                    //most recent heading
                                    gpsHeading = Math.Atan2(pn.fix.easting - stepFixPts[currentStepFix].easting,
                                    pn.fix.northing - stepFixPts[currentStepFix].northing);

                                    //Pointing the opposite way the fixes are moving
                                    //if (vehicle.isReverse) gpsHeading += Math.PI;
                                    if (gpsHeading < 0) gpsHeading += glm.twoPI;
                                    if (gpsHeading >= glm.twoPI) gpsHeading -= glm.twoPI;

                                    //set the headings
                                    fixHeading = gpsHeading;
                                    camHeading = glm.toDegrees(gpsHeading);
                                }

                                fixStepDist = distanceCurrentStepFix;

                                // IMU Fusion with heading correction, add the correction
                                if (ahrs.isHeadingCorrectionFromBrick | ahrs.isHeadingCorrectionFromAutoSteer) //| ahrs.isHeadingCorrectionFromExtUDP
                                {
                                    //current gyro angle in radians
                                    double correctionHeading = (glm.toRadians((double)ahrs.correctionHeadingX16 * 0.0625));

                                    //Difference between the IMU heading and the GPS heading
                                    double gyroDelta = (correctionHeading + gyroCorrection) - gpsHeading;
                                    if (gyroDelta < 0) gyroDelta += glm.twoPI;

                                    //calculate delta based on circular data problem 0 to 360 to 0, clamp to +- 2 Pi
                                    if (gyroDelta >= -glm.PIBy2 && gyroDelta <= glm.PIBy2) gyroDelta *= -1.0;
                                    else
                                    {
                                        if (gyroDelta > glm.PIBy2) { gyroDelta = glm.twoPI - gyroDelta; }
                                        else { gyroDelta = (glm.twoPI + gyroDelta) * -1.0; }
                                    }
                                    if (gyroDelta > glm.twoPI) gyroDelta -= glm.twoPI;
                                    if (gyroDelta < -glm.twoPI) gyroDelta += glm.twoPI;

                                    //if the gyro and last corrected fix is < 10 degrees, super low pass for gps
                                    if (Math.Abs(gyroDelta) < 0.18)
                                    {
                                        //a bit of delta and add to correction to current gyro
                                        gyroCorrection += (gyroDelta * (ahrs.fusionWeight / fixUpdateHz));
                                        if (gyroCorrection > glm.twoPI) gyroCorrection -= glm.twoPI;
                                        if (gyroCorrection < -glm.twoPI) gyroCorrection += glm.twoPI;
                                    }
                                    else
                                    {
                                        //a bit of delta and add to correction to current gyro
                                        gyroCorrection += (gyroDelta * (2.0 / fixUpdateHz));
                                        if (gyroCorrection > glm.twoPI) gyroCorrection -= glm.twoPI;
                                        if (gyroCorrection < -glm.twoPI) gyroCorrection += glm.twoPI;
                                    }

                                    //determine the Corrected heading based on gyro and GPS
                                    gyroCorrected = correctionHeading + gyroCorrection;
                                    if (gyroCorrected > glm.twoPI) gyroCorrected -= glm.twoPI;
                                    if (gyroCorrected < 0) gyroCorrected += glm.twoPI;

                                    fixHeading = gyroCorrected;

                                    camHeading = fixHeading;
                                    if (camHeading > glm.twoPI) camHeading -= glm.twoPI;
                                    camHeading = glm.toDegrees(camHeading);
                                }

                                //Calculate a million other things
                                TheRest();

                            }
                            //else
                            //{
                            //    //we have come to a stop so save last valid heading
                            //    SaveLastValidHeadingAndReset();
                            //}
                            break;
                        }

                    case "GPS":
                        {
                            if (pn.speed > 0.6)
                            {
                                //use NMEA headings for camera and tractor graphic
                                fixHeading = glm.toRadians(pn.headingTrue);
                                camHeading = pn.headingTrue;
                                gpsHeading = fixHeading;
                            }

                            //grab the most current fix to last fix distance
                            distanceCurrentStepFix = glm.Distance(pn.fix, prevFix);


                            //an IMU with heading correction, add the correction
                            if (ahrs.isHeadingCorrectionFromBrick | ahrs.isHeadingCorrectionFromAutoSteer)
                            {
                                //current gyro angle in radians
                                double correctionHeading = (glm.toRadians((double)ahrs.correctionHeadingX16 * 0.0625));

                                //Difference between the IMU heading and the GPS heading
                                double gyroDelta = (correctionHeading + gyroCorrection) - gpsHeading;
                                if (gyroDelta < 0) gyroDelta += glm.twoPI;

                                //calculate delta based on circular data problem 0 to 360 to 0, clamp to +- 2 Pi
                                if (gyroDelta >= -glm.PIBy2 && gyroDelta <= glm.PIBy2) gyroDelta *= -1.0;
                                else
                                {
                                    if (gyroDelta > glm.PIBy2) { gyroDelta = glm.twoPI - gyroDelta; }
                                    else { gyroDelta = (glm.twoPI + gyroDelta) * -1.0; }
                                }
                                if (gyroDelta > glm.twoPI) gyroDelta -= glm.twoPI;
                                if (gyroDelta < -glm.twoPI) gyroDelta += glm.twoPI;

                                //if the gyro and last corrected fix is < 10 degrees, super low pass for gps
                                if (Math.Abs(gyroDelta) < 0.18)
                                {
                                    //a bit of delta and add to correction to current gyro
                                    gyroCorrection += (gyroDelta * (ahrs.fusionWeight / fixUpdateHz));
                                    if (gyroCorrection > glm.twoPI) gyroCorrection -= glm.twoPI;
                                    if (gyroCorrection < -glm.twoPI) gyroCorrection += glm.twoPI;
                                }
                                else
                                {
                                    //a bit of delta and add to correction to current gyro
                                    gyroCorrection += (gyroDelta * (2.0 / fixUpdateHz));
                                    if (gyroCorrection > glm.twoPI) gyroCorrection -= glm.twoPI;
                                    if (gyroCorrection < -glm.twoPI) gyroCorrection += glm.twoPI;
                                }

                                //determine the Corrected heading based on gyro and GPS
                                gyroCorrected = correctionHeading + gyroCorrection;
                                if (gyroCorrected > glm.twoPI) gyroCorrected -= glm.twoPI;
                                if (gyroCorrected < 0) gyroCorrected += glm.twoPI;

                                fixHeading = gyroCorrected;

                                camHeading = fixHeading;
                                if (camHeading > glm.twoPI) camHeading -= glm.twoPI;
                                camHeading = glm.toDegrees(camHeading);
                            }

                            #region Antenna Offset

                            if (vehicle.antennaOffset != 0)
                            {
                                pn.fix.easting = (Math.Cos(-fixHeading) * vehicle.antennaOffset) + pn.fix.easting;
                                pn.fix.northing = (Math.Sin(-fixHeading) * vehicle.antennaOffset) + pn.fix.northing;
                            }
                            #endregion

                            #region Roll

                            rollUsed = 0;

                            if (ahrs.isRollFromAutoSteer)
                            {
                                rollUsed = ((double)(ahrs.rollX16 - ahrs.rollZeroX16)) * 0.0625;

                                //change for roll to the right is positive times -1
                                rollCorrectionDistance = Math.Sin(glm.toRadians((rollUsed))) * -vehicle.antennaHeight;

                                // roll to left is positive  **** important!!
                                // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                                pn.fix.easting = (Math.Cos(-fixHeading) * rollCorrectionDistance) + pn.fix.easting;
                                pn.fix.northing = (Math.Sin(-fixHeading) * rollCorrectionDistance) + pn.fix.northing;
                            }

                            #endregion Roll

                            TheRest();

                            //most recent fixes are now the prev ones
                            prevFix.easting = pn.fix.easting; prevFix.northing = pn.fix.northing;

                            break;
                        }

                    case "Dual":
                        {
                            //use Dual Antenna heading for camera and tractor graphic
                            fixHeading = glm.toRadians(pn.headingHDT);
                            camHeading = pn.headingHDT;
                            gpsHeading = fixHeading;

                            //grab the most current fix and save the distance from the last fix
                            distanceCurrentStepFix = glm.Distance(pn.fix, prevFix);

                            if (vehicle.antennaOffset != 0)
                            {
                                pn.fix.easting = (Math.Cos(-fixHeading) * vehicle.antennaOffset) + pn.fix.easting;
                                pn.fix.northing = (Math.Sin(-fixHeading) * vehicle.antennaOffset) + pn.fix.northing;
                            }

                            //used only for draft compensation in OGI Sentence
                            if (ahrs.isRollFromOGI) rollUsed = ((double)(ahrs.rollX16 - ahrs.rollZeroX16)) * 0.0625;

                            // roll from AVR
                            else
                            {
                                rollUsed = ((double)(ahrs.rollX16 - ahrs.rollZeroX16)) * 0.0625;

                                //change for roll to the right is positive times -1
                                rollCorrectionDistance = Math.Sin(glm.toRadians((rollUsed))) * -vehicle.antennaHeight;

                                // roll to left is positive  **** important!!
                                // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                                pn.fix.easting = (Math.Cos(-fixHeading) * rollCorrectionDistance) + pn.fix.easting;
                                pn.fix.northing = (Math.Sin(-fixHeading) * rollCorrectionDistance) + pn.fix.northing;
                            }

                            TheRest();

                            //most recent fixes are now the prev ones
                            prevFix.easting = pn.fix.easting; prevFix.northing = pn.fix.northing;

                            break;
                        }

                    default:
                        break;

                }            

            #region AutoSteer

            //preset the values
            guidanceLineDistanceOff = 32000;

            if (ct.isContourBtnOn)
            {
                ct.DistanceFromContourLine(pivotAxlePos, steerAxlePos);
            }
            else
            {
                if (curve.isCurveSet)
                {
                    //do the calcs for AB Curve
                    curve.GetCurrentCurveLine(pivotAxlePos, steerAxlePos);
                }

                if (ABLine.isABLineSet)
                {
                    ABLine.GetCurrentABLine(pivotAxlePos, steerAxlePos);
                    if (yt.isRecordingCustomYouTurn)
                    {
                        //save reference of first point
                        if (yt.youFileList.Count == 0)
                        {
                            vec2 start = new vec2(steerAxlePos.easting, steerAxlePos.northing);
                            yt.youFileList.Add(start);
                        }
                        else
                        {
                            //keep adding points
                            vec2 point = new vec2(steerAxlePos.easting - yt.youFileList[0].easting, steerAxlePos.northing - yt.youFileList[0].northing);
                            yt.youFileList.Add(point);
                        }
                    }
                }
            }

            // autosteer at full speed of updates
            if (!isAutoSteerBtnOn) //32020 means auto steer is off
            {
                guidanceLineDistanceOff = 32020;
            }

            // If Drive button enabled be normal, or just fool the autosteer and fill values
            if (!ast.isInFreeDriveMode)
            {
                //sidehill draft compensation
                if (rollUsed != 0)
                {
                    guidanceLineSteerAngle = (Int16)(guidanceLineSteerAngle +
                        ((-rollUsed) * ((double)mc.autoSteerSettings[mc.ssKd] / 50)) * 500);
                }

                //fill up0 the appropriate arrays with new values
                mc.autoSteerData[mc.sdSpeed] = unchecked((byte)(Math.Abs(pn.speed) * 4.0));
                //mc.machineControlData[mc.cnSpeed] = mc.autoSteerData[mc.sdSpeed];

                mc.autoSteerData[mc.sdDistanceHi] = unchecked((byte)(guidanceLineDistanceOff >> 8));
                mc.autoSteerData[mc.sdDistanceLo] = unchecked((byte)(guidanceLineDistanceOff));

                mc.autoSteerData[mc.sdSteerAngleHi] = unchecked((byte)(guidanceLineSteerAngle >> 8));
                mc.autoSteerData[mc.sdSteerAngleLo] = unchecked((byte)(guidanceLineSteerAngle));
            }

            else
            {
                //fill up the auto steer array with free drive values
                mc.autoSteerData[mc.sdSpeed] = unchecked((byte)(pn.speed * 4.0 + 16));
                //mc.machineControlData[mc.cnSpeed] = mc.autoSteerData[mc.sdSpeed];

                //make steer module think everything is normal
                guidanceLineDistanceOff = 0;
                mc.autoSteerData[mc.sdDistanceHi] = unchecked((byte)(0));
                mc.autoSteerData[mc.sdDistanceLo] = unchecked((byte)0);

                guidanceLineSteerAngle = (Int16)(ast.driveFreeSteerAngle * 100);
                mc.autoSteerData[mc.sdSteerAngleHi] = unchecked((byte)(guidanceLineSteerAngle >> 8));
                mc.autoSteerData[mc.sdSteerAngleLo] = unchecked((byte)(guidanceLineSteerAngle));
            }

            //out serial to autosteer module  //indivdual classes load the distance and heading deltas 
            SendOutUSBAutoSteerPort(mc.autoSteerData, CModuleComm.pgnSentenceLength);

            //send out to network
            if (Properties.Settings.Default.setUDP_isOn)
            {
                //send autosteer since it never is logic controlled
                SendUDPMessage(mc.autoSteerData);

                //machine control
                SendUDPMessage(mc.machineData);
            }

            //for average cross track error
            if (guidanceLineDistanceOff < 29000)
            {
                crossTrackError = (int)((double)crossTrackError * 0.90 + Math.Abs((double)guidanceLineDistanceOff) * 0.1);
            }
            else
            {
                crossTrackError = 0;
            }

            #endregion

            #region Youturn

            //reset the fault distance to an appropriate weird number
            //-2222 means it fell out of the loop completely
            //-3333 means unable to find a nearest point at all even though inside the work area of field
            // -4444 means cross trac error too high
            distancePivotToTurnLine = -4444;

            //always force out of bounds and change only if in bounds after proven so
            mc.isOutOfBounds = true;

            //if an outer boundary is set, then apply critical stop logic
            if (bnd.bndArr.Count > 0)
            {
                //Are we inside outer and outside inner all turn boundaries, no turn creation problems
                if (IsInsideGeoFenceAKABoundary() && !yt.isTurnCreationTooClose && !yt.isTurnCreationNotCrossingError)
                {
                    //reset critical stop for bounds violation
                    mc.isOutOfBounds = false;

                    //do the auto youturn logic if everything is on.
                    if (yt.isYouTurnBtnOn && isAutoSteerBtnOn)
                    {
                        //if we are too much off track > 1.3m, kill the diagnostic creation, start again
                        if (crossTrackError > 1300 && !yt.isYouTurnTriggered)
                        {
                            yt.ResetCreatedYouTurn();
                        }
                        else
                        {
                            //now check to make sure we are not in an inner turn boundary - drive thru is ok
                            if (yt.youTurnPhase != 3)
                            {
                                if (crossTrackError > 500)
                                {
                                    yt.ResetCreatedYouTurn();
                                }
                                else
                                {
                                    if (yt.isUsingDubinsTurn)
                                    {
                                        if (ABLine.isABLineSet) yt.BuildABLineDubinsYouTurn(yt.isYouTurnRight);
                                        else yt.BuildCurveDubinsYouTurn(yt.isYouTurnRight, pivotAxlePos);
                                    }
                                    else
                                    {
                                        if (ABLine.isABLineSet) yt.BuildABLinePatternYouTurn(yt.isYouTurnRight);
                                        else yt.BuildCurvePatternYouTurn(yt.isYouTurnRight, pivotAxlePos);
                                    }
                                }
                            }
                            else //wait to trigger the actual turn since its made and waiting
                            {
                                //distance from current pivot to first point of youturn pattern
                                distancePivotToTurnLine = glm.Distance(yt.ytList[0], steerAxlePos);

                                if ((distancePivotToTurnLine <= 20.0) && (distancePivotToTurnLine >= 18.0) && !yt.isYouTurnTriggered)

                                    if (!isBoundAlarming)
                                    {
                                        sndBoundaryAlarm.Play();
                                        isBoundAlarming = true;
                                    }

                                //if we are close enough to pattern, trigger.
                                if ((distancePivotToTurnLine <= 1.0) && (distancePivotToTurnLine >= 0) && !yt.isYouTurnTriggered)
                                {
                                    yt.YouTurnTrigger();
                                    isBoundAlarming = false;
                                }
                            }
                        }
                    } // end of isInWorkingArea
                }
                // here is stop logic for out of bounds - in an inner or out the outer turn border.
                else
                {
                    mc.isOutOfBounds = true;
                    if (yt.isYouTurnBtnOn)
                    {
                        yt.ResetCreatedYouTurn();
                        sim.stepDistance = 0 / 17.86;
                    }
                }
            }
            else
            {
                mc.isOutOfBounds = false;
            }

            #endregion

            #region Remote Switches

            // by MTZ8302 ------------------------------------------------------------------------------------
            if (mc.ss[mc.swHeaderLo] == 249) DoRemoteSwitches();

            #endregion

            //update main window
            oglMain.MakeCurrent();
            oglMain.Refresh();

            //end of UppdateFixPosition
            swFrame.Stop();

            //stop the timer and calc how long it took to do calcs and draw
            frameTimeRough = (double)swFrame.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000;

            frameTime = frameTime * 0.99 + frameTimeRough * 0.01;
        }

        double frameTimeRough = 3;
        private void TheRest()
        {
            //positions and headings 
            CalculatePositionHeading();

            //calculate lookahead at full speed, no sentence misses
            CalculateSectionLookAhead(toolPos.northing, toolPos.easting, cosSectionHeading, sinSectionHeading);

            //To prevent drawing high numbers of triangles, determine and test before drawing vertex
            sectionTriggerDistance = glm.Distance(pn.fix, prevSectionPos);

            //section on off and points, contour points
            if (sectionTriggerDistance > sectionTriggerStepDistance && isJobStarted)
            {
                AddSectionOrContourPathPoints();

                //grab fix and elevation
                if (isLogElevation) sbFix.Append(pn.fix.easting.ToString("N2") + "," + pn.fix.northing.ToString("N2") + ","
                                                    + pn.altitude.ToString("N2") + ","
                                                    + pn.latitude + "," + pn.longitude + "\r\n");
            }

            //test if travelled far enough for new boundary point
            if (bnd.isOkToAddPoints)
            {
                double boundaryDistance = glm.Distance(pn.fix, prevBoundaryPos);
                if (boundaryDistance > 1) AddBoundaryPoint();
            }

            //calc distance travelled since last GPS fix
            distance = glm.Distance(pn.fix, prevFix);
            if ((fd.distanceUser += distance) > 3000) fd.distanceUser = 0; ;//userDistance can be reset
        }

        public bool isBoundAlarming;

        //all the hitch, pivot, section, trailing hitch, headings and fixes
        private void CalculatePositionHeading()
        {
            #region pivot hitch trail

            //translate from pivot position to steer axle and pivot axle position
            if (pn.speed > -0.1)
            {

                //translate world to the pivot axle
                pivotAxlePos.easting = pn.fix.easting - (Math.Sin(fixHeading) * vehicle.antennaPivot);
                pivotAxlePos.northing = pn.fix.northing - (Math.Cos(fixHeading) * vehicle.antennaPivot);
                pivotAxlePos.heading = fixHeading;
                steerAxlePos.easting = pivotAxlePos.easting + (Math.Sin(fixHeading) * vehicle.wheelbase);
                steerAxlePos.northing = pivotAxlePos.northing + (Math.Cos(fixHeading) * vehicle.wheelbase);
                steerAxlePos.heading = fixHeading;
            }
            else
            {
                //translate world to the pivot axle
                pivotAxlePos.easting = pn.fix.easting - (Math.Sin(fixHeading) * -vehicle.antennaPivot);
                pivotAxlePos.northing = pn.fix.northing - (Math.Cos(fixHeading) * -vehicle.antennaPivot);
                pivotAxlePos.heading = fixHeading;
                steerAxlePos.easting = pivotAxlePos.easting + (Math.Sin(fixHeading) * -vehicle.wheelbase);
                steerAxlePos.northing = pivotAxlePos.northing + (Math.Cos(fixHeading) * -vehicle.wheelbase);
                steerAxlePos.heading = fixHeading;
            }

            //determine where the rigid vehicle hitch ends
            hitchPos.easting = pn.fix.easting + (Math.Sin(fixHeading) * (tool.hitchLength - vehicle.antennaPivot));
            hitchPos.northing = pn.fix.northing + (Math.Cos(fixHeading) * (tool.hitchLength - vehicle.antennaPivot));

            //tool attached via a trailing hitch
            if (tool.isToolTrailing)
            {
                double over;
                if (tool.isToolTBT)
                {
                    //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                    if (distanceCurrentStepFix != 0)
                    {
                        double t = (tool.toolTankTrailingHitchLength) / distanceCurrentStepFix;
                        tankPos.easting = hitchPos.easting + t * (hitchPos.easting - tankPos.easting);
                        tankPos.northing = hitchPos.northing + t * (hitchPos.northing - tankPos.northing);
                        tankPos.heading = Math.Atan2(hitchPos.easting - tankPos.easting, hitchPos.northing - tankPos.northing);
                    }

                    ////the tool is seriously jacknifed or just starting out so just spring it back.
                    over = Math.Abs(Math.PI - Math.Abs(Math.Abs(tankPos.heading - fixHeading) - Math.PI));

                    if (over < 2.0 && startCounter > 50)
                    {
                        tankPos.easting = hitchPos.easting + (Math.Sin(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                        tankPos.northing = hitchPos.northing + (Math.Cos(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                    }

                    //criteria for a forced reset to put tool directly behind vehicle
                    if (over > 2.0 | startCounter < 51)
                    {
                        tankPos.heading = fixHeading;
                        tankPos.easting = hitchPos.easting + (Math.Sin(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                        tankPos.northing = hitchPos.northing + (Math.Cos(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                    }
                }
                else
                {
                    tankPos.heading = fixHeading;
                    tankPos.easting = hitchPos.easting;
                    tankPos.northing = hitchPos.northing;
                }

                //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                if (distanceCurrentStepFix != 0)
                {
                    double t = (tool.toolTrailingHitchLength) / distanceCurrentStepFix;
                    toolPos.easting = tankPos.easting + t * (tankPos.easting - toolPos.easting);
                    toolPos.northing = tankPos.northing + t * (tankPos.northing - toolPos.northing);
                    toolPos.heading = Math.Atan2(tankPos.easting - toolPos.easting, tankPos.northing - toolPos.northing);
                }

                ////the tool is seriously jacknifed or just starting out so just spring it back.
                over = Math.Abs(Math.PI - Math.Abs(Math.Abs(toolPos.heading - tankPos.heading) - Math.PI));

                if (over < 1.9 && startCounter > 50)
                {
                    toolPos.easting = tankPos.easting + (Math.Sin(toolPos.heading) * (tool.toolTrailingHitchLength));
                    toolPos.northing = tankPos.northing + (Math.Cos(toolPos.heading) * (tool.toolTrailingHitchLength));
                }

                //criteria for a forced reset to put tool directly behind vehicle
                if (over > 1.9 | startCounter < 51)
                {
                    toolPos.heading = tankPos.heading;
                    toolPos.easting = tankPos.easting + (Math.Sin(toolPos.heading) * (tool.toolTrailingHitchLength));
                    toolPos.northing = tankPos.northing + (Math.Cos(toolPos.heading) * (tool.toolTrailingHitchLength));
                }
            }

            //rigidly connected to vehicle
            else
            {
                toolPos.heading = fixHeading;
                toolPos.easting = hitchPos.easting;
                toolPos.northing = hitchPos.northing;
            }

            #endregion

            //used to increase triangle count when going around corners, less on straight
            //pick the slow moving side edge of tool
            double distance = tool.toolWidth * 0.5;
            if (distance > 3) distance = 3;
            
            //whichever is less
            if (tool.toolFarLeftSpeed < tool.toolFarRightSpeed)
            {
                double twist = tool.toolFarLeftSpeed / tool.toolFarRightSpeed;
                //twist *= twist;
                if (twist < 0.2) twist = 0.2;
                sectionTriggerStepDistance = distance * twist * twist;
            }
            else
            {
                double twist = tool.toolFarRightSpeed / tool.toolFarLeftSpeed;
                //twist *= twist;
                if (twist < 0.2) twist = 0.2;

                sectionTriggerStepDistance = distance * twist * twist;
            }

            //finally fixed distance for making a curve line
            if (!curve.isOkToAddPoints) sectionTriggerStepDistance = sectionTriggerStepDistance + 0.2;
            else sectionTriggerStepDistance = 1.0;

            //precalc the sin and cos of heading * -1
            sinSectionHeading = Math.Sin(-toolPos.heading);
            cosSectionHeading = Math.Cos(-toolPos.heading);
        }

        //perimeter and boundary point generation
        public void AddBoundaryPoint()
        {
            //save the north & east as previous
            prevBoundaryPos.easting = pn.fix.easting;
            prevBoundaryPos.northing = pn.fix.northing;

            //build the boundary line

            if (bnd.isOkToAddPoints)
            {
                if (bnd.isDrawRightSide)
                {
                    //Right side
                    vec3 point = new vec3(
                        pivotAxlePos.easting + (Math.Sin(pivotAxlePos.heading - glm.PIBy2) * -bnd.createBndOffset),
                        pivotAxlePos.northing + (Math.Cos(pivotAxlePos.heading - glm.PIBy2) * -bnd.createBndOffset), 
                        pivotAxlePos.heading);
                    bnd.bndBeingMadePts.Add(point);
                }

                //draw on left side
                else
                {
                    //Right side
                    vec3 point = new vec3(
                        pivotAxlePos.easting + (Math.Sin(pivotAxlePos.heading - glm.PIBy2) * bnd.createBndOffset),
                        pivotAxlePos.northing + (Math.Cos(pivotAxlePos.heading - glm.PIBy2) * bnd.createBndOffset), 
                        pivotAxlePos.heading);
                    bnd.bndBeingMadePts.Add(point);
                }
            }
        }

        //add the points for section, contour line points, Area Calc feature
        private void AddSectionOrContourPathPoints()
        {
            if (curve.isOkToAddPoints)
            {
                vec3 pt = new vec3(pivotAxlePos.easting, pivotAxlePos.northing, pivotAxlePos.heading);
                curve.refList.Add(pt);
            }

            //save the north & east as previous
            prevSectionPos.northing = pn.fix.northing;
            prevSectionPos.easting = pn.fix.easting;

            // if non zero, at least one section is on.
            int sectionCounter = 0;

            //send the current and previous GPS fore/aft corrected fix to each section
            for (int j = 0; j < tool.numOfSections + 1; j++)
            {
                if (section[j].isMappingOn)
                {
                    section[j].AddMappingPoint();
                    sectionCounter++;
                }
            }
            if ((ABLine.isBtnABLineOn && !ct.isContourBtnOn && ABLine.isABLineSet && isAutoSteerBtnOn) ||
                        (!ct.isContourBtnOn && curve.isBtnCurveOn && curve.isCurveSet && isAutoSteerBtnOn))
            {
                //no contour recorded
                if (ct.isContourOn) { ct.StopContourLine(steerAxlePos); }
            }
            else
            {
                //Contour Base Track.... At least One section on, turn on if not
                if (sectionCounter != 0)
                {
                    //keep the line going, everything is on for recording path
                    if (ct.isContourOn) ct.AddPoint(pivotAxlePos);
                    else
                    {
                        ct.StartContourLine(pivotAxlePos);
                        ct.AddPoint(pivotAxlePos);
                    }
                }

                //All sections OFF so if on, turn off
                else { if (ct.isContourOn) { ct.StopContourLine(pivotAxlePos); } }

                //Build contour line if close enough to a patch
                if (ct.isContourBtnOn) ct.BuildContourGuidanceLine(pivotAxlePos);
            }

        }

        //calculate the extreme tool left, right velocities, each section lookahead, and whether or not its going backwards
        public void CalculateSectionLookAhead(double northing, double easting, double cosHeading, double sinHeading)
        {
            //calculate left side of section 1
            vec3 left = new vec3();
            vec3 right = left;
            double leftSpeed = 0, rightSpeed = 0;

            //speed max for section kmh*0.277 to m/s * 10 cm per pixel * 1.7 max speed
            double meterPerSecPerPixel = Math.Abs(pn.speed) * 4.5;

            //now loop all the section rights and the one extreme left
            for (int j = 0; j < tool.numOfSections; j++)
            {
                if (j == 0)
                {
                    //only one first left point, the rest are all rights moved over to left
                    section[j].leftPoint = new vec3(cosHeading * (section[j].positionLeft) + easting,
                                       sinHeading * (section[j].positionLeft) + northing,0);

                    left = section[j].leftPoint - section[j].lastLeftPoint;

                    //save a copy for next time
                    section[j].lastLeftPoint = section[j].leftPoint;

                    //get the speed for left side only once
                    
                    leftSpeed = left.GetLength() / fixUpdateTime * 10;
                    if (leftSpeed > meterPerSecPerPixel) leftSpeed = meterPerSecPerPixel;

                }
                else
                {
                    //right point from last section becomes this left one
                    section[j].leftPoint = section[j - 1].rightPoint;
                    left = section[j].leftPoint - section[j].lastLeftPoint;

                    //save a copy for next time
                    section[j].lastLeftPoint = section[j].leftPoint;
                    
                    //Save the slower of the 2
                    if (leftSpeed > rightSpeed) leftSpeed = rightSpeed;                    
                }

                section[j].rightPoint = new vec3(cosHeading * (section[j].positionRight) + easting,
                                    sinHeading * (section[j].positionRight) + northing,0);

                //now we have left and right for this section
                right = section[j].rightPoint - section[j].lastRightPoint;

                //save a copy for next time
                section[j].lastRightPoint = section[j].rightPoint;

                //grab vector length and convert to meters/sec/10 pixels per meter                
                rightSpeed = right.GetLength() / fixUpdateTime * 10;
                if (rightSpeed > meterPerSecPerPixel) rightSpeed = meterPerSecPerPixel;

                //Is section outer going forward or backward
                double head = left.HeadingXZ();
                if (Math.PI - Math.Abs(Math.Abs(head - toolPos.heading) - Math.PI) > glm.PIBy2)
                {
                    if (leftSpeed > 0) leftSpeed *= -1;
                }

                head = right.HeadingXZ();
                if (Math.PI - Math.Abs(Math.Abs(head - toolPos.heading) - Math.PI) > glm.PIBy2)
                {
                    if (rightSpeed > 0) rightSpeed *= -1;
                }

                double sped = 0;
                //save the far left and right speed in m/sec averaged over 20%
                if (j==0)
                {
                    sped = (leftSpeed * 0.1);
                    if (sped < 0.1) sped = 0.1;
                    tool.toolFarLeftSpeed = tool.toolFarLeftSpeed * 0.9 + sped * 0.1;
                }
                if (j == tool.numOfSections - 1)
                {
                    sped = (rightSpeed * 0.1);
                    if (sped < 0.1) sped = 0.1;
                    tool.toolFarRightSpeed = tool.toolFarRightSpeed * 0.9 + sped * 0.1;
                }

                //choose fastest speed
                if (leftSpeed > rightSpeed)
                {
                    sped = leftSpeed;
                    leftSpeed = rightSpeed;
                }
                else sped = rightSpeed;
                section[j].speedPixels = section[j].speedPixels * 0.9 + sped * 0.1;
            }

            //fill in tool positions
            section[tool.numOfSections].leftPoint = section[0].leftPoint;
            section[tool.numOfSections].rightPoint = section[tool.numOfSections-1].rightPoint;

            //set the look ahead for hyd Lift in pixels per second
            vehicle.hydLiftLookAheadDistanceLeft = tool.toolFarLeftSpeed * vehicle.hydLiftLookAheadTime * 10;
            vehicle.hydLiftLookAheadDistanceRight = tool.toolFarRightSpeed * vehicle.hydLiftLookAheadTime * 10;

            if (vehicle.hydLiftLookAheadDistanceLeft > 200) vehicle.hydLiftLookAheadDistanceLeft = 200;
            if (vehicle.hydLiftLookAheadDistanceRight > 200) vehicle.hydLiftLookAheadDistanceRight = 200;

            tool.lookAheadDistanceOnPixelsLeft = tool.toolFarLeftSpeed * tool.lookAheadOnSetting * 10;
            tool.lookAheadDistanceOnPixelsRight = tool.toolFarRightSpeed * tool.lookAheadOnSetting * 10;

            if (tool.lookAheadDistanceOnPixelsLeft > 200) tool.lookAheadDistanceOnPixelsLeft = 200;
            if (tool.lookAheadDistanceOnPixelsRight > 200) tool.lookAheadDistanceOnPixelsRight = 200;

            tool.lookAheadDistanceOffPixelsLeft = tool.toolFarLeftSpeed * tool.lookAheadOffSetting * 10;
            tool.lookAheadDistanceOffPixelsRight = tool.toolFarRightSpeed * tool.lookAheadOffSetting * 10;

            if (tool.lookAheadDistanceOffPixelsLeft > 160) tool.lookAheadDistanceOffPixelsLeft = 160;
            if (tool.lookAheadDistanceOffPixelsRight > 160) tool.lookAheadDistanceOffPixelsRight = 160;

            //determine where the tool is wrt to headland
            if (hd.isOn) hd.WhereAreToolCorners();

            //set up the super for youturn
            section[tool.numOfSections].isInBoundary = true;

            //determine if section is in boundary and headland using the section left/right positions
            bool isLeftIn = true, isRightIn = true;

            for (int j = 0; j < tool.numOfSections; j++)
            {
                if (bnd.bndArr.Count > 0)
                {
                    if (j == 0)
                    {
                        //only one first left point, the rest are all rights moved over to left
                        isLeftIn = bnd.bndArr[0].IsPointInsideBoundaryEar(section[j].leftPoint);
                        isRightIn = bnd.bndArr[0].IsPointInsideBoundaryEar(section[j].rightPoint);

                        for (int i = 1; i < bnd.bndArr.Count; i++)
                        {
                            //inner boundaries should normally NOT have point inside
                            if (bnd.bndArr[i].isSet)
                            {
                                isLeftIn &= !bnd.bndArr[i].IsPointInsideBoundaryEar(section[j].leftPoint);
                                isRightIn &= !bnd.bndArr[i].IsPointInsideBoundaryEar(section[j].rightPoint);
                            }
                        }

                        //merge the two sides into in or out
                        if (isLeftIn && isRightIn) section[j].isInBoundary = true;
                        else section[j].isInBoundary = false;
                    }

                    else
                    {
                        //grab the right of previous section, its the left of this section
                        isLeftIn = isRightIn;
                        isRightIn = bnd.bndArr[0].IsPointInsideBoundaryEar(section[j].rightPoint);
                        for (int i = 1; i < bnd.bndArr.Count; i++)
                        {
                            //inner boundaries should normally NOT have point inside
                            if (bnd.bndArr[i].isSet) isRightIn &= !bnd.bndArr[i].IsPointInsideBoundaryEar(section[j].rightPoint);
                        }

                        if (isLeftIn && isRightIn) section[j].isInBoundary = true;
                        else section[j].isInBoundary = false;
                    }
                    section[tool.numOfSections].isInBoundary &= section[j].isInBoundary;

                }

                //no boundary created so always inside
                else
                {
                    section[j].isInBoundary = true;
                    section[tool.numOfSections].isInBoundary = false;
                }
            }
        }

        //the start of first few frames to initialize entire program
        private void InitializeFirstFewGPSPositions()
        {
            if (!isFirstFixPositionSet)
            {
                pn.latStart = pn.latitude; pn.lonStart = pn.longitude;

                pn.SetLocalMetersPerDegree();

                pn.ConvertWGS84ToLocal(pn.latitude, pn.longitude, out pn.fix.northing, out pn.fix.easting);

                //Draw a grid once we know where in the world we are.
                isFirstFixPositionSet = true;
                worldGrid.CreateWorldGrid(pn.fix.northing, pn.fix.easting);

                //most recent fixes
                prevFix.easting = pn.fix.easting;
                prevFix.northing = pn.fix.northing;

                //run once and return
                isFirstFixPositionSet = true;

                //set up the modules
                mc.ResetAllModuleCommValues();

                //SendSteerSettingsOutAutoSteerPort();
                //SendArduinoSettingsOutToAutoSteerPort();
                return;
            }

            else
            {
                prevFix.easting = pn.fix.easting; prevFix.northing = pn.fix.northing;

                //keep here till valid data
                if (startCounter > (totalFixSteps / 2.0))
                {
                    isGPSPositionInitialized = true;
                }

                //in radians
                fixHeading = 0;
                toolPos.heading = fixHeading;

                //send out initial zero settings
                if (isGPSPositionInitialized)
                {
                    //set up the modules
                    mc.ResetAllModuleCommValues();

                    //SendSteerSettingsOutAutoSteerPort();
                    //SendArduinoSettingsOutToAutoSteerPort();
                }

                //send out initial zero settings
                if (isGPSPositionInitialized)
                {
                    //set up the modules
                    mc.ResetAllModuleCommValues();

                    //SendSteerSettingsOutAutoSteerPort();
                    //SendArduinoSettingsOutToAutoSteerPort();

                    IsBetweenSunriseSunset(pn.latitude, pn.longitude);

                    //set display accordingly
                    isDayTime = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);

                    if (isAutoDayNight)
                    {
                        isDay = isDayTime;
                        isDay = !isDay;
                        SwapDayNightMode();
                    }
                }
                return;
            }
        }

        private void DoRemoteSwitches()
        {
            //MTZ8302 Feb 2020 
            if (isJobStarted)
            {
                //MainSW was used
                if (mc.ss[mc.swMain] != mc.ssP[mc.swMain])
                {
                    //Main SW pressed
                    if ((mc.ss[mc.swMain] & 1) == 1)
                    {
                        //set butto off and then press it = ON
                        autoBtnState = btnStates.Off;
                        btnSectionOffAutoOn.PerformClick();
                    } // if Main SW ON

                    //if Main SW in Arduino is pressed OFF
                    if ((mc.ss[mc.swMain] & 2) == 2)
                    {
                        //set button on and then press it = OFF
                        autoBtnState = btnStates.Auto;
                        btnSectionOffAutoOn.PerformClick();
                    } // if Main SW OFF

                    mc.ssP[mc.swMain] = mc.ss[mc.swMain];
                }  //Main or Rate SW


                if (mc.ss[mc.swONLo] != 0)
                {
                    // ON Signal from Arduino 
                    if ((mc.ss[mc.swONLo] & 128) == 128 & tool.numOfSections > 7)
                    {
                        if (section[7].manBtnState != manBtn.Auto) section[7].manBtnState = manBtn.Auto;
                        btnSection8Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONLo] & 64) == 64 & tool.numOfSections > 6)
                    {
                        if (section[6].manBtnState != manBtn.Auto) section[6].manBtnState = manBtn.Auto;
                        btnSection7Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONLo] & 32) == 32 & tool.numOfSections > 5)
                    {
                        if (section[5].manBtnState != manBtn.Auto) section[5].manBtnState = manBtn.Auto;
                        btnSection6Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONLo] & 16) == 16 & tool.numOfSections > 4)
                    {
                        if (section[4].manBtnState != manBtn.Auto) section[4].manBtnState = manBtn.Auto;
                        btnSection5Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONLo] & 8) == 8 & tool.numOfSections > 3)
                    {
                        if (section[3].manBtnState != manBtn.Auto) section[3].manBtnState = manBtn.Auto;
                        btnSection4Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONLo] & 4) == 4 & tool.numOfSections > 2)
                    {
                        if (section[2].manBtnState != manBtn.Auto) section[2].manBtnState = manBtn.Auto;
                        btnSection3Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONLo] & 2) == 2 & tool.numOfSections > 1)
                    {
                        if (section[1].manBtnState != manBtn.Auto) section[1].manBtnState = manBtn.Auto;
                        btnSection2Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONLo] & 1) == 1)
                    {
                        if (section[0].manBtnState != manBtn.Auto) section[0].manBtnState = manBtn.Auto;
                        btnSection1Man.PerformClick();
                    }
                    mc.ssP[mc.swONLo] = mc.ss[mc.swONLo];
                } //if swONLo != 0 
                else { if (mc.ssP[mc.swONLo] != 0) { mc.ssP[mc.swONLo] = 0; } }

                if (mc.ss[mc.swONHi] != 0)
                {
                    // sections ON signal from Arduino  
                    if ((mc.ss[mc.swONHi] & 128) == 128 & tool.numOfSections > 15)
                    {
                        if (section[15].manBtnState != manBtn.Auto) section[15].manBtnState = manBtn.Auto;
                        btnSection16Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONHi] & 64) == 64 & tool.numOfSections > 14)
                    {
                        if (section[14].manBtnState != manBtn.Auto) section[14].manBtnState = manBtn.Auto;
                        btnSection15Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONHi] & 32) == 32 & tool.numOfSections > 13)
                    {
                        if (section[13].manBtnState != manBtn.Auto) section[13].manBtnState = manBtn.Auto;
                        btnSection14Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONHi] & 16) == 16 & tool.numOfSections > 12)
                    {
                        if (section[12].manBtnState != manBtn.Auto) section[12].manBtnState = manBtn.Auto;
                        btnSection13Man.PerformClick();
                    }

                    if ((mc.ss[mc.swONHi] & 8) == 8 & tool.numOfSections > 11)
                    {
                        if (section[11].manBtnState != manBtn.Auto) section[11].manBtnState = manBtn.Auto;
                        btnSection12Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONHi] & 4) == 4 & tool.numOfSections > 10)
                    {
                        if (section[10].manBtnState != manBtn.Auto) section[10].manBtnState = manBtn.Auto;
                        btnSection11Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONHi] & 2) == 2 & tool.numOfSections > 9)
                    {
                        if (section[9].manBtnState != manBtn.Auto) section[9].manBtnState = manBtn.Auto;
                        btnSection10Man.PerformClick();
                    }
                    if ((mc.ss[mc.swONHi] & 1) == 1 & tool.numOfSections > 8)
                    {
                        if (section[8].manBtnState != manBtn.Auto) section[8].manBtnState = manBtn.Auto;
                        btnSection9Man.PerformClick();
                    }
                    mc.ssP[mc.swONHi] = mc.ss[mc.swONHi];
                } //if swONHi != 0   
                else { if (mc.ssP[mc.swONHi] != 0) { mc.ssP[mc.swONHi] = 0; } }

                // Switches have changed
                if (mc.ss[mc.swOFFLo] != mc.ssP[mc.swOFFLo])
                {
                    //if Main = Auto then change section to Auto if Off signal from Arduino stopped
                    if (autoBtnState == btnStates.Auto)
                    {
                        if (((mc.ssP[mc.swOFFLo] & 128) == 128) & ((mc.ss[mc.swOFFLo] & 128) != 128) & (section[7].manBtnState == manBtn.Off))
                        {
                            btnSection8Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFLo] & 64) == 64) & ((mc.ss[mc.swOFFLo] & 64) != 64) & (section[6].manBtnState == manBtn.Off))
                        {
                            btnSection7Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFLo] & 32) == 32) & ((mc.ss[mc.swOFFLo] & 32) != 32) & (section[5].manBtnState == manBtn.Off))
                        {
                            btnSection6Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFLo] & 16) == 16) & ((mc.ss[mc.swOFFLo] & 16) != 16) & (section[4].manBtnState == manBtn.Off))
                        {
                            btnSection5Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFLo] & 8) == 8) & ((mc.ss[mc.swOFFLo] & 8) != 8) & (section[3].manBtnState == manBtn.Off))
                        {
                            btnSection4Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFLo] & 4) == 4) & ((mc.ss[mc.swOFFLo] & 4) != 4) & (section[2].manBtnState == manBtn.Off))
                        {
                            btnSection3Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFLo] & 2) == 2) & ((mc.ss[mc.swOFFLo] & 2) != 2) & (section[1].manBtnState == manBtn.Off))
                        {
                            btnSection2Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFLo] & 1) == 1) & ((mc.ss[mc.swOFFLo] & 1) != 1) & (section[0].manBtnState == manBtn.Off))
                        {
                            btnSection1Man.PerformClick();
                        }
                    }
                    mc.ssP[mc.swOFFLo] = mc.ss[mc.swOFFLo];
                }

                if (mc.ss[mc.swOFFHi] != mc.ssP[mc.swOFFHi])
                {
                    //if Main = Auto then change section to Auto if Off signal from Arduino stopped
                    if (autoBtnState == btnStates.Auto)
                    {
                        if (((mc.ssP[mc.swOFFHi] & 128) == 128) & ((mc.ss[mc.swOFFHi] & 128) != 128) & (section[15].manBtnState == manBtn.Off))
                        { btnSection16Man.PerformClick(); }

                        if (((mc.ssP[mc.swOFFHi] & 64) == 64) & ((mc.ss[mc.swOFFHi] & 64) != 64) & (section[14].manBtnState == manBtn.Off))
                        { btnSection15Man.PerformClick(); }

                        if (((mc.ssP[mc.swOFFHi] & 32) == 32) & ((mc.ss[mc.swOFFHi] & 32) != 32) & (section[13].manBtnState == manBtn.Off))
                        { btnSection14Man.PerformClick(); }

                        if (((mc.ssP[mc.swOFFHi] & 16) == 16) & ((mc.ss[mc.swOFFHi] & 16) != 16) & (section[12].manBtnState == manBtn.Off))
                        { btnSection13Man.PerformClick(); }


                        if (((mc.ssP[mc.swOFFHi] & 8) == 8) & ((mc.ss[mc.swOFFHi] & 8) != 8) & (section[11].manBtnState == manBtn.Off))
                        {
                            btnSection12Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFHi] & 4) == 4) & ((mc.ss[mc.swOFFHi] & 4) != 4) & (section[10].manBtnState == manBtn.Off))
                        {
                            btnSection11Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFHi] & 2) == 2) & ((mc.ss[mc.swOFFHi] & 2) != 2) & (section[9].manBtnState == manBtn.Off))
                        {
                            btnSection10Man.PerformClick();
                        }
                        if (((mc.ssP[mc.swOFFHi] & 1) == 1) & ((mc.ss[mc.swOFFHi] & 1) != 1) & (section[8].manBtnState == manBtn.Off))
                        {
                            btnSection9Man.PerformClick();
                        }
                    }
                    mc.ssP[mc.swOFFHi] = mc.ss[mc.swOFFHi];
                }

                // OFF Signal from Arduino
                if (mc.ss[mc.swOFFLo] != 0)
                {
                    //if section SW in Arduino is switched to OFF; check always, if switch is locked to off GUI should not change
                    if ((mc.ss[mc.swOFFLo] & 128) == 128 & section[7].manBtnState != manBtn.Off)
                    {
                        section[7].manBtnState = manBtn.On;
                        btnSection8Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFLo] & 64) == 64 & section[6].manBtnState != manBtn.Off)
                    {
                        section[6].manBtnState = manBtn.On;
                        btnSection7Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFLo] & 32) == 32 & section[5].manBtnState != manBtn.Off)
                    {
                        section[5].manBtnState = manBtn.On;
                        btnSection6Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFLo] & 16) == 16 & section[4].manBtnState != manBtn.Off)
                    {
                        section[4].manBtnState = manBtn.On;
                        btnSection5Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFLo] & 8) == 8 & section[3].manBtnState != manBtn.Off)
                    {
                        section[3].manBtnState = manBtn.On;
                        btnSection4Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFLo] & 4) == 4 & section[2].manBtnState != manBtn.Off)
                    {
                        section[2].manBtnState = manBtn.On;
                        btnSection3Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFLo] & 2) == 2 & section[1].manBtnState != manBtn.Off)
                    {
                        section[1].manBtnState = manBtn.On;
                        btnSection2Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFLo] & 1) == 1 & section[0].manBtnState != manBtn.Off)
                    {
                        section[0].manBtnState = manBtn.On;
                        btnSection1Man.PerformClick();
                    }
                } // if swOFFLo !=0
                if (mc.ss[mc.swOFFHi] != 0)
                {
                    //if section SW in Arduino is switched to OFF; check always, if switch is locked to off GUI should not change
                    if ((mc.ss[mc.swOFFHi] & 128) == 128 & section[15].manBtnState != manBtn.Off)
                    {
                        section[15].manBtnState = manBtn.On;
                        btnSection16Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFHi] & 64) == 64 & section[14].manBtnState != manBtn.Off)
                    {
                        section[14].manBtnState = manBtn.On;
                        btnSection15Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFHi] & 32) == 32 & section[13].manBtnState != manBtn.Off)
                    {
                        section[13].manBtnState = manBtn.On;
                        btnSection14Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFHi] & 16) == 16 & section[12].manBtnState != manBtn.Off)
                    {
                        section[12].manBtnState = manBtn.On;
                        btnSection13Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFHi] & 8) == 8 & section[11].manBtnState != manBtn.Off)
                    {
                        section[11].manBtnState = manBtn.On;
                        btnSection12Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFHi] & 4) == 4 & section[10].manBtnState != manBtn.Off)
                    {
                        section[10].manBtnState = manBtn.On;
                        btnSection11Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFHi] & 2) == 2 & section[9].manBtnState != manBtn.Off)
                    {
                        section[9].manBtnState = manBtn.On;
                        btnSection10Man.PerformClick();
                    }
                    if ((mc.ss[mc.swOFFHi] & 1) == 1 & section[8].manBtnState != manBtn.Off)
                    {
                        section[8].manBtnState = manBtn.On;
                        btnSection9Man.PerformClick();
                    }
                } // if swOFFHi !=0

            //set to make sure new data arrives
            mc.ss[mc.swHeaderLo] = 0;

            }//if serial or udp port open
        }

        public bool IsInsideGeoFenceAKABoundary()
        {
            //first where are we, must be inside outer and outside of inner geofence non drive thru turn borders
            if (bnd.bndArr[0].IsPointInsideBoundary(pivotAxlePos))
            {
                for (int i = 1; i < bnd.bndArr.Count; i++)
                {
                    //make sure not inside a non drivethru boundary
                    if (!bnd.bndArr[i].isSet) continue;
                    if (bnd.bndArr[i].isDriveThru) continue;
                    if (bnd.bndArr[i].IsPointInsideBoundary(pivotAxlePos))
                    {
                        distancePivotToTurnLine = -3333;
                        return false;
                    }
                }
            }
            else
            {
                distancePivotToTurnLine = -3333;
                return false;
            }
            //we are safely inside outer, outside inner boundaries
            return true;
        }       

    }//end class
}//end namespace

////its a drive thru inner boundary
//else
//{

//    if (distPivot < yt.triggerDistance && distPivot > (yt.triggerDistance - 2.0) && !yt.isEnteringDriveThru && !yt.isInboundary && isBndInWay)
//    {
//        //our direction heading into turn
//        //yt.youTurnTriggerPoint = pivotAxlePos;
//        yt.isEnteringDriveThru = true;
//        headlandAngleOffPerpendicular = Math.PI - Math.Abs(Math.Abs(hl.closestHeadlandPt.heading - pivotAxlePos.heading) - Math.PI);
//        if (headlandAngleOffPerpendicular < 0) headlandAngleOffPerpendicular += glm.twoPI;
//        //while (headlandAngleOffPerpendicular > 1.57) headlandAngleOffPerpendicular -= 1.57;
//        headlandAngleOffPerpendicular -= glm.PIBy2;
//        headlandDistanceDelta = Math.Tan(Math.Abs(headlandAngleOffPerpendicular));
//        headlandDistanceDelta *= tool.toolWidth;
//    }

//    if (yt.isEnteringDriveThru)
//    {
//        int c = 0;
//        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
//        {
//            //checked for any not triggered yet (false) - if there is, not done yet
//            if (!seq.seqEnter[i].isTrig) c++;
//        }

//        if (c == 0)
//        {
//            //sequences all done so reset everything
//            //yt.isSequenceTriggered = false;
//            yt.whereAmI = 0;
//            yt.ResetSequenceEventTriggers();
//            distTool = -2222;
//            yt.isEnteringDriveThru = false;
//            yt.isExitingDriveThru = true;
//            //yt.youTurnTriggerPoint = pivotAxlePos;
//        }
//    }

//    if (yt.isExitingDriveThru)
//    {
//        int c = 0;
//        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
//        {
//            //checked for any not triggered yet (false) - if there is, not done yet
//            if (!seq.seqExit[i].isTrig) c++;
//        }

//        if (c == 0)
//        {
//            //sequences all done so reset everything
//            //yt.isSequenceTriggered = false;
//            yt.whereAmI = 0;
//            yt.ResetSequenceEventTriggers();
//            distTool = -2222;
//            yt.isEnteringDriveThru = false;
//            yt.isExitingDriveThru = false;
//            yt.youTurnTriggerPoint = pivotAxlePos;
//        }
//    }
//}

//Do the sequencing of functions around the turn.
//if (yt.isSequenceTriggered) yt.DoSequenceEvent();

//do sequencing for drive thru boundaries
//if (yt.isEnteringDriveThru || yt.isExitingDriveThru) yt.DoDriveThruSequenceEvent();

//else //make sure youturn and sequence is off - we are not in normal turn here
//{
//    if (yt.isYouTurnTriggered | yt.isSequenceTriggered)
//    {
//        yt.ResetYouTurn();
//    }
//}
