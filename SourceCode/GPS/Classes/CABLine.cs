﻿using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CABLine
    {
        public double abFixHeadingDelta;
        public double abHeading;
        public double angVel;
        //the current AB guidance line
        public vec2 currentABLineP1 = new vec2(0.0, 0.0);

        public vec2 currentABLineP2 = new vec2(0.0, 1.0);
        public double distanceFromCurrentLine;
        public double distanceFromRefLine;
        //pure pursuit values
        public vec2 goalPointAB = new vec2(0, 0);

        //List of all available ABLines
        public List<CABLines> lineArr = new List<CABLines>();

        public int numABLines, numABLineSelected;

        public double howManyPathsAway, moveDistance;
        public bool isABLineBeingSet, isEditing;
        public bool isABLineSet, isABLineLoaded;
        public bool isABSameAsVehicleHeading = true;
        public bool isBtnABLineOn;
        public bool isOnRightSideCurrentLine = true;

        //public bool isOnTramLine;
        //public int tramBasedOn;
        public double passNumber;
        public double ppRadiusAB;
        public vec2 radiusPointAB = new vec2(0, 0);
        public double rEastAB, rNorthAB;
        //the reference line endpoints
        public vec2 refABLineP1 = new vec2(0.0, 0.0);
        public vec2 refABLineP2 = new vec2(0.0, 1.0);
        
        public double refLineSide = 1.0;
        //the two inital A and B points
        public vec2 refPoint1 = new vec2(0.2, 0.15);
        public vec2 refPoint2 = new vec2(0.3, 0.3);

        public double snapDistance;
        public double steerAngleAB;
        public int lineWidth;

        //tramlines
        public List<vec2> tramArr = new List<vec2>();
        public List<List<vec2>> tramList = new List<List<vec2>>();

        //Color tramColor = Color.YellowGreen;
        public int tramPassEvery;
        //pointers to mainform controls
        private readonly FormGPS mf;

        public CABLine(FormGPS _f)
        {
            //constructor
            mf = _f;
            //isOnTramLine = true;
            lineWidth = Properties.Settings.Default.setDisplay_lineWidth;
        }

        public void DrawABLines()
        {
            //Draw AB Points
            GL.PointSize(8.0f);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0.95f, 0.0f, 0.0f);
            GL.Vertex3(refPoint1.easting, refPoint1.northing, 0.0);
            GL.Color3(0.0f, 0.90f, 0.95f);
            GL.Vertex3(refPoint2.easting, refPoint2.northing, 0.0);
            GL.End();
            
            if (mf.font.isFontOn)
            {
                mf.font.DrawText3D(refPoint1.easting, refPoint1.northing, "&A");
                mf.font.DrawText3D(refPoint2.easting, refPoint2.northing, "&B");
            }

            GL.PointSize(1.0f);

            //Draw reference AB line
            GL.LineWidth(lineWidth);
            GL.Enable(EnableCap.LineStipple);
            GL.LineStipple(1, 0x0F00);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(0.930f, 0.2f, 0.2f);
            GL.Vertex3(refABLineP1.easting, refABLineP1.northing, 0);
            GL.Vertex3(refABLineP2.easting, refABLineP2.northing, 0);
            GL.End();
            GL.Disable(EnableCap.LineStipple);

            //draw current AB Line
            GL.LineWidth(lineWidth);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(0.95f, 0.0f, 0.950f);
            GL.Vertex3(currentABLineP1.easting, currentABLineP1.northing, 0.0);
            GL.Vertex3(currentABLineP2.easting, currentABLineP2.northing, 0.0);
            GL.End();


            if (!isEditing)
            {
                if (mf.isSideGuideLines && mf.camera.camSetDistance > mf.tool.toolWidth * -120)
                {
                    //get the tool offset and width
                    double toolOffset = mf.tool.toolOffset * 2;
                    double toolWidth = mf.tool.toolWidth - mf.tool.toolOverlap;
                    double cosHeading = Math.Cos(-abHeading);
                    double sinHeading = Math.Sin(-abHeading);

                    GL.Color3(0.56f, 0.650f, 0.650f);
                    GL.Enable(EnableCap.LineStipple);
                    GL.LineStipple(1, 0x0101);

                    GL.LineWidth(lineWidth);
                    GL.Begin(PrimitiveType.Lines);

                    if (isABSameAsVehicleHeading)
                    {
                        GL.Vertex3((cosHeading * (toolWidth + toolOffset)) + currentABLineP1.easting, (sinHeading * (toolWidth + toolOffset)) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * (toolWidth + toolOffset)) + currentABLineP2.easting, (sinHeading * (toolWidth + toolOffset)) + currentABLineP2.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth + toolOffset)) + currentABLineP1.easting, (sinHeading * (-toolWidth + toolOffset)) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth + toolOffset)) + currentABLineP2.easting, (sinHeading * (-toolWidth + toolOffset)) + currentABLineP2.northing, 0);

                        toolWidth *= 2;
                        GL.Vertex3((cosHeading * toolWidth) + currentABLineP1.easting, (sinHeading * toolWidth) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * toolWidth) + currentABLineP2.easting, (sinHeading * toolWidth) + currentABLineP2.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth)) + currentABLineP1.easting, (sinHeading * (-toolWidth)) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth)) + currentABLineP2.easting, (sinHeading * (-toolWidth)) + currentABLineP2.northing, 0);
                    }
                    else
                    {
                        GL.Vertex3((cosHeading * (toolWidth - toolOffset)) + currentABLineP1.easting, (sinHeading * (toolWidth - toolOffset)) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * (toolWidth - toolOffset)) + currentABLineP2.easting, (sinHeading * (toolWidth - toolOffset)) + currentABLineP2.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth - toolOffset)) + currentABLineP1.easting, (sinHeading * (-toolWidth - toolOffset)) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth - toolOffset)) + currentABLineP2.easting, (sinHeading * (-toolWidth - toolOffset)) + currentABLineP2.northing, 0);

                        toolWidth *= 2;
                        GL.Vertex3((cosHeading * toolWidth) + currentABLineP1.easting, (sinHeading * toolWidth) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * toolWidth) + currentABLineP2.easting, (sinHeading * toolWidth) + currentABLineP2.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth)) + currentABLineP1.easting, (sinHeading * (-toolWidth)) + currentABLineP1.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth)) + currentABLineP2.easting, (sinHeading * (-toolWidth)) + currentABLineP2.northing, 0);
                    }

                    GL.End();
                    GL.Disable(EnableCap.LineStipple);
                }
            }

            if (isEditing)
            {
                double toolWidth2 = mf.tool.toolWidth - mf.tool.toolOverlap;
                double cosHeading2 = Math.Cos(-mf.ABLine.abHeading);
                double sinHeading2 = Math.Sin(-mf.ABLine.abHeading);

                if (mf.camera.camSetDistance > -200)
                {
                    GL.Color3(0.9630f, 0.2f, 0.2f);
                    GL.LineWidth(mf.ABLine.lineWidth);
                    GL.Enable(EnableCap.LineStipple);
                    GL.LineStipple(1, 0x0707);

                    GL.Begin(PrimitiveType.Lines);

                    for (int i = 1; i <= 6; i++)
                    {
                        GL.Vertex3((cosHeading2 * toolWidth2) + mf.ABLine.refABLineP1.easting, (sinHeading2 * toolWidth2) + mf.ABLine.refABLineP1.northing, 0);
                        GL.Vertex3((cosHeading2 * toolWidth2) + mf.ABLine.refABLineP2.easting, (sinHeading2 * toolWidth2) + mf.ABLine.refABLineP2.northing, 0);
                        toolWidth2 = toolWidth2 + mf.tool.toolWidth - mf.tool.toolOverlap;
                    }

                    GL.End();
                    GL.Disable(EnableCap.LineStipple);
                }
            }

            if (mf.isPureDisplayOn && !mf.isStanleyUsed)
            {
                //Draw lookahead Point
                GL.PointSize(8.0f);
                GL.Begin(PrimitiveType.Points);
                GL.Color3(1.0f, 1.0f, 0.0f);
                GL.Vertex3(goalPointAB.easting, goalPointAB.northing, 0.0);
                //GL.Vertex3(rEastAB, rNorthAB, 0.0);
                GL.End();
                GL.PointSize(1.0f);
            }

            mf.yt.DrawYouTurn();

            if (mf.yt.isRecordingCustomYouTurn)
            {
                GL.Color3(0.05f, 0.05f, 0.95f);
                GL.PointSize(2.0f);
                int ptCount = mf.yt.youFileList.Count;
                if (ptCount > 1)
                {
                    GL.Begin(PrimitiveType.Points);
                    for (int i = 1; i < ptCount; i++)
                    {
                        GL.Vertex3(mf.yt.youFileList[i].easting + mf.yt.youFileList[0].easting, mf.yt.youFileList[i].northing + mf.yt.youFileList[0].northing, 0);
                    }
                    GL.End();
                }
            }
            
            GL.PointSize(1.0f);
            GL.LineWidth(1);

            if (mf.tram.displayMode == 1 || mf.tram.displayMode == 2 )DrawTram();
            if (mf.tram.displayMode == 1 || mf.tram.displayMode == 3) mf.tram.DrawTramBnd();
        }

        public void DrawTram()
        {
            GL.Color4(0.8630f, 0.93692f, 0.3260f, 0.22);
            for (int i = 0; i < tramList.Count; i++)
            {
                GL.Begin(PrimitiveType.TriangleStrip);
                for (int h = 0; h < tramList[i].Count; h++) 
                    GL.Vertex3(tramList[i][h].easting, tramList[i][h].northing, 0);
                GL.End();
            }

            //draw tram numbers at end and beggining of line
            if (mf.font.isFontOn)
            {

                for (int i = 0; i < tramList.Count; i++)
                {
                    int middle = 0;
                    GL.Color4(0.8630f, 0.93692f, 0.8260f, 0.752);
                    if (tramList[i].Count > 1)
                    {
                        middle = tramList[i].Count - 1;
                        mf.font.DrawText3D(tramList[i][middle].easting, tramList[i][middle].northing, (i + 1).ToString());
                        mf.font.DrawText3D(tramList[i][0].easting, tramList[i][0].northing, (i + 1).ToString());

                    }
                }
            }
        }

        public void BuildTram()
        {
            mf.tram.BuildTramBnd();

            tramList?.Clear();
            tramArr?.Clear();
            List<vec2> tramRef = new List<vec2>();

            bool isBndExist = mf.bnd.bndArr.Count != 0;

            double pass = 0.5;
            double hsin = Math.Sin(abHeading);
            double hcos = Math.Cos(abHeading);

            //divide up the AB line into segments
            vec2 P1 = new vec2();
            for (int i = 0; i < 3200; i += 4)
            {
                P1.easting = (hsin * i) + refABLineP1.easting;
                P1.northing = (hcos * i) + refABLineP1.northing;
                tramRef.Add(P1);
            }

            //create list of list of points of triangle strip of AB Highlight
            double headingCalc = abHeading + glm.PIBy2;
            hsin = Math.Sin(headingCalc);
            hcos = Math.Cos(headingCalc);

            tramList?.Clear();
            tramArr?.Clear();

            for (int i = 0; i < mf.tram.passes; i++)
            {
                tramArr = new List<vec2>();
                tramList.Add(tramArr);

                for (int j = 0; j < tramRef.Count; j++)
                {
                    P1.easting =  (hsin * ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tram.abOffset)) + tramRef[j].easting;
                    P1.northing = (hcos * ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tram.abOffset)) + tramRef[j].northing;

                    if (isBndExist)
                    {
                        if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(P1))
                        {
                            tramArr.Add(P1);
                            P1.easting =  (hsin * mf.tram.wheelTrack) + P1.easting;
                            P1.northing = (hcos * mf.tram.wheelTrack) + P1.northing;
                            tramArr.Add(P1);
                        }
                    }
                    else
                    {
                        tramArr.Add(P1);

                        P1.easting =  (hsin * mf.tram.wheelTrack) + P1.easting;
                        P1.northing = (hcos * mf.tram.wheelTrack) + P1.northing;
                        tramArr.Add(P1);
                    }
                }
            }

            tramRef?.Clear();
            //outside tram

            if (mf.bnd.bndArr.Count == 0 || mf.tram.passes != 0)
            {                               
                //return;
            }
        }

        public void GetCurrentABLine(vec3 pivot, vec3 steer)
        {

            if (mf.isStanleyUsed)
            {
                //move the ABLine over based on the overlap amount set in vehicle
                double widthMinusOverlap = mf.tool.toolWidth - mf.tool.toolOverlap;

                //x2-x1
                double dx = refABLineP2.easting - refABLineP1.easting;
                //z2-z1
                double dy = refABLineP2.northing - refABLineP1.northing;

                //how far are we away from the reference line at 90 degrees
                distanceFromRefLine = ((dy * pivot.easting) - (dx * pivot.northing) + (refABLineP2.easting
                                        * refABLineP1.northing) - (refABLineP2.northing * refABLineP1.easting))
                                            / Math.Sqrt((dy * dy) + (dx * dx));

                //sign of distance determines which side of line we are on
                if (distanceFromRefLine > 0) refLineSide = 1;
                else refLineSide = -1;

                //absolute the distance
                distanceFromRefLine = Math.Abs(distanceFromRefLine);

                //Which ABLine is the vehicle on, negative is left and positive is right side
                howManyPathsAway = Math.Round(distanceFromRefLine / widthMinusOverlap, 0, MidpointRounding.AwayFromZero);

                //generate that pass number as signed integer
                passNumber = Convert.ToInt32(refLineSide * howManyPathsAway);

                //calculate the new point that is number of implement widths over
                double toolOffset = mf.tool.toolOffset;
                vec2 point1;

                //depending which way you are going, the offset can be either side
                if (isABSameAsVehicleHeading)
                {
                    point1 = new vec2((Math.Cos(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) - toolOffset)) + refPoint1.easting,
                    (Math.Sin(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) - toolOffset)) + refPoint1.northing);
                }
                else
                {
                    point1 = new vec2((Math.Cos(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) + toolOffset)) + refPoint1.easting,
                        (Math.Sin(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) + toolOffset)) + refPoint1.northing);
                }

                //create the new line extent points for current ABLine based on original heading of AB line
                currentABLineP1.easting = point1.easting - (Math.Sin(abHeading) * 1600.0);
                currentABLineP1.northing = point1.northing - (Math.Cos(abHeading) * 1600.0);

                currentABLineP2.easting = point1.easting + (Math.Sin(abHeading) * 1600.0);
                currentABLineP2.northing = point1.northing + (Math.Cos(abHeading) * 1600.0);

                //get the distance from currently active AB line
                //x2-x1
                dx = currentABLineP2.easting - currentABLineP1.easting;
                //z2-z1
                dy = currentABLineP2.northing - currentABLineP1.northing;

                //save a copy of dx,dy in youTurn
                mf.yt.dxAB = dx; mf.yt.dyAB = dy;

                //how far from current AB Line is fix
                distanceFromCurrentLine = ((dy * steer.easting) - (dx * steer.northing) + (currentABLineP2.easting
                            * currentABLineP1.northing) - (currentABLineP2.northing * currentABLineP1.easting))
                            / Math.Sqrt((dy * dy) + (dx * dx));

                //are we on the right side or not
                isOnRightSideCurrentLine = distanceFromCurrentLine > 0;

                //absolute the distance
                distanceFromCurrentLine = Math.Abs(distanceFromCurrentLine);

                //Subtract the two headings, if > 1.57 its going the opposite heading as refAB
                abFixHeadingDelta = (Math.Abs(mf.fixHeading - abHeading));
                if (abFixHeadingDelta >= Math.PI) abFixHeadingDelta = Math.Abs(abFixHeadingDelta - glm.twoPI);

                isABSameAsVehicleHeading = abFixHeadingDelta < glm.PIBy2;

                // **Stanley Point ** - calc point on ABLine closest to current steer position
                double U = (((steer.easting - currentABLineP1.easting) * dx)
                            + ((steer.northing - currentABLineP1.northing) * dy))
                            / ((dx * dx) + (dy * dy));

                //point on AB line closest to pivot axle point
                rEastAB = currentABLineP1.easting + (U * dx);
                rNorthAB = currentABLineP1.northing + (U * dy);

                //distance is negative if on left, positive if on right
                if (isABSameAsVehicleHeading)
                {
                    if (!isOnRightSideCurrentLine)
                    {
                        distanceFromCurrentLine *= -1.0;
                    }
                    abFixHeadingDelta = (steer.heading - abHeading);
                }

                //opposite way so right is left
                else
                {
                    if (isOnRightSideCurrentLine)
                    {
                        distanceFromCurrentLine *= -1.0;
                    }
                    abFixHeadingDelta = (steer.heading - abHeading + Math.PI);
                }

                //Fix the circular error
                if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
                else if (abFixHeadingDelta < Math.PI) abFixHeadingDelta += Math.PI;

                if (abFixHeadingDelta > glm.PIBy2) abFixHeadingDelta -= Math.PI;
                else if (abFixHeadingDelta < -glm.PIBy2) abFixHeadingDelta += Math.PI;

                abFixHeadingDelta *= mf.vehicle.stanleyHeadingErrorGain;
                if (abFixHeadingDelta > 0.4) abFixHeadingDelta = 0.4;
                if (abFixHeadingDelta < -0.4) abFixHeadingDelta = -0.4;

                steerAngleAB = Math.Atan((distanceFromCurrentLine * mf.vehicle.stanleyGain)
                    / ((Math.Abs(mf.pn.speed * 0.277777)) + 1));

                if (steerAngleAB > 0.4) steerAngleAB = 0.4;
                if (steerAngleAB < -0.4) steerAngleAB = -0.4;

                if (mf.pn.speed > -0.1)
                    steerAngleAB = glm.toDegrees((steerAngleAB + abFixHeadingDelta) * -1.0);
                else
                    steerAngleAB = glm.toDegrees((steerAngleAB - abFixHeadingDelta) * -1.0);

                if (steerAngleAB < -mf.vehicle.maxSteerAngle) steerAngleAB = -mf.vehicle.maxSteerAngle;
                if (steerAngleAB > mf.vehicle.maxSteerAngle) steerAngleAB = mf.vehicle.maxSteerAngle;

                //Convert to millimeters
                distanceFromCurrentLine = Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);
            }
            else
            {
                //move the ABLine over based on the overlap amount set in vehicle
                double widthMinusOverlap = mf.tool.toolWidth - mf.tool.toolOverlap;

                //x2-x1
                double dx = refABLineP2.easting - refABLineP1.easting;
                //z2-z1
                double dy = refABLineP2.northing - refABLineP1.northing;

                //how far are we away from the reference line at 90 degrees
                distanceFromRefLine = ((dy * pivot.easting) - (dx * pivot.northing) + (refABLineP2.easting
                                        * refABLineP1.northing) - (refABLineP2.northing * refABLineP1.easting))
                                            / Math.Sqrt((dy * dy) + (dx * dx));

                //sign of distance determines which side of line we are on
                if (distanceFromRefLine > 0) refLineSide = 1;
                else refLineSide = -1;

                //absolute the distance
                distanceFromRefLine = Math.Abs(distanceFromRefLine);

                //Which ABLine is the vehicle on, negative is left and positive is right side
                howManyPathsAway = Math.Round(distanceFromRefLine / widthMinusOverlap, 0, MidpointRounding.AwayFromZero);

                //generate that pass number as signed integer
                passNumber = Convert.ToInt32(refLineSide * howManyPathsAway);

                //calculate the new point that is number of implement widths over
                double toolOffset = mf.tool.toolOffset;
                vec2 point1;

                //depending which way you are going, the offset can be either side
                if (isABSameAsVehicleHeading)
                {
                    point1 = new vec2((Math.Cos(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) - toolOffset)) + refPoint1.easting,
                    (Math.Sin(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) - toolOffset)) + refPoint1.northing);
                }
                else
                {
                    point1 = new vec2((Math.Cos(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) + toolOffset)) + refPoint1.easting,
                        (Math.Sin(-abHeading) * ((widthMinusOverlap * howManyPathsAway * refLineSide) + toolOffset)) + refPoint1.northing);
                }

                //create the new line extent points for current ABLine based on original heading of AB line
                currentABLineP1.easting = point1.easting - (Math.Sin(abHeading) * 1600.0);
                currentABLineP1.northing = point1.northing - (Math.Cos(abHeading) * 1600.0);

                currentABLineP2.easting = point1.easting + (Math.Sin(abHeading) * 1600.0);
                currentABLineP2.northing = point1.northing + (Math.Cos(abHeading) * 1600.0);

                //get the distance from currently active AB line
                //x2-x1
                dx = currentABLineP2.easting - currentABLineP1.easting;
                //z2-z1
                dy = currentABLineP2.northing - currentABLineP1.northing;

                //save a copy of dx,dy in youTurn
                mf.yt.dxAB = dx; mf.yt.dyAB = dy;

                //how far from current AB Line is fix
                distanceFromCurrentLine = ((dy * pivot.easting) - (dx * pivot.northing) + (currentABLineP2.easting
                            * currentABLineP1.northing) - (currentABLineP2.northing * currentABLineP1.easting))
                            / Math.Sqrt((dy * dy) + (dx * dx));

                //are we on the right side or not
                isOnRightSideCurrentLine = distanceFromCurrentLine > 0;

                //absolute the distance
                distanceFromCurrentLine = Math.Abs(distanceFromCurrentLine);

                //update base on autosteer settings and distance from line
                double goalPointDistance = mf.vehicle.UpdateGoalPointDistance(distanceFromCurrentLine);
                mf.lookaheadActual = goalPointDistance;

                //Subtract the two headings, if > 1.57 its going the opposite heading as refAB
                abFixHeadingDelta = (Math.Abs(mf.fixHeading - abHeading));
                if (abFixHeadingDelta >= Math.PI) abFixHeadingDelta = Math.Abs(abFixHeadingDelta - glm.twoPI);

                // ** Pure pursuit ** - calc point on ABLine closest to current position
                double U = (((pivot.easting - currentABLineP1.easting) * dx)
                            + ((pivot.northing - currentABLineP1.northing) * dy))
                            / ((dx * dx) + (dy * dy));

                //point on AB line closest to pivot axle point
                rEastAB = currentABLineP1.easting + (U * dx);
                rNorthAB = currentABLineP1.northing + (U * dy);

                if (abFixHeadingDelta >= glm.PIBy2)
                {
                    isABSameAsVehicleHeading = false;
                    goalPointAB.easting = rEastAB - (Math.Sin(abHeading) * goalPointDistance);
                    goalPointAB.northing = rNorthAB - (Math.Cos(abHeading) * goalPointDistance);
                }
                else
                {
                    isABSameAsVehicleHeading = true;
                    goalPointAB.easting = rEastAB + (Math.Sin(abHeading) * goalPointDistance);
                    goalPointAB.northing = rNorthAB + (Math.Cos(abHeading) * goalPointDistance);
                }

                //calc "D" the distance from pivot axle to lookahead point
                double goalPointDistanceDSquared
                    = glm.DistanceSquared(goalPointAB.northing, goalPointAB.easting, pivot.northing, pivot.easting);

                //calculate the the new x in local coordinates and steering angle degrees based on wheelbase
                double localHeading = glm.twoPI - mf.fixHeading;
                ppRadiusAB = goalPointDistanceDSquared / (2 * (((goalPointAB.easting - pivot.easting) * Math.Cos(localHeading))
                    + ((goalPointAB.northing - pivot.northing) * Math.Sin(localHeading))));

                steerAngleAB = glm.toDegrees(Math.Atan(2 * (((goalPointAB.easting - pivot.easting) * Math.Cos(localHeading))
                    + ((goalPointAB.northing - pivot.northing) * Math.Sin(localHeading))) * mf.vehicle.wheelbase
                    / goalPointDistanceDSquared));
                if (steerAngleAB < -mf.vehicle.maxSteerAngle) steerAngleAB = -mf.vehicle.maxSteerAngle;
                if (steerAngleAB > mf.vehicle.maxSteerAngle) steerAngleAB = mf.vehicle.maxSteerAngle;

                //limit circle size for display purpose
                if (ppRadiusAB < -500) ppRadiusAB = -500;
                if (ppRadiusAB > 500) ppRadiusAB = 500;

                radiusPointAB.easting = pivot.easting + (ppRadiusAB * Math.Cos(localHeading));
                radiusPointAB.northing = pivot.northing + (ppRadiusAB * Math.Sin(localHeading));

                //Convert to millimeters
                distanceFromCurrentLine = Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);

                //angular velocity in rads/sec  = 2PI * m/sec * radians/meters
                angVel = glm.twoPI * 0.277777 * mf.pn.speed * (Math.Tan(glm.toRadians(steerAngleAB))) / mf.vehicle.wheelbase;

                //clamp the steering angle to not exceed safe angular velocity
                if (Math.Abs(angVel) > mf.vehicle.maxAngularVelocity)
                {
                    steerAngleAB = glm.toDegrees(steerAngleAB > 0 ? (Math.Atan((mf.vehicle.wheelbase * mf.vehicle.maxAngularVelocity)
                        / (glm.twoPI * mf.pn.speed * 0.277777)))
                        : (Math.Atan((mf.vehicle.wheelbase * -mf.vehicle.maxAngularVelocity) / (glm.twoPI * mf.pn.speed * 0.277777))));
                }

                //distance is negative if on left, positive if on right
                if (isABSameAsVehicleHeading)
                {
                    if (!isOnRightSideCurrentLine) distanceFromCurrentLine *= -1.0;
                }

                //opposite way so right is left
                else
                {
                    if (isOnRightSideCurrentLine) distanceFromCurrentLine *= -1.0;
                }
            }

            mf.guidanceLineDistanceOff = mf.distanceDisplay = (Int16)distanceFromCurrentLine;
            mf.guidanceLineSteerAngle = (Int16)(steerAngleAB * 100);

            if (mf.yt.isYouTurnTriggered)
            {
                //do the pure pursuit from youTurn
                mf.yt.DistanceFromYouTurnLine();

                //now substitute what it thinks are AB line values with auto turn values
                steerAngleAB = mf.yt.steerAngleYT;
                distanceFromCurrentLine = mf.yt.distanceFromCurrentLine;

                goalPointAB = mf.yt.goalPointYT;
                radiusPointAB.easting = mf.yt.radiusPointYT.easting;
                radiusPointAB.northing = mf.yt.radiusPointYT.northing;
                ppRadiusAB = mf.yt.ppRadiusYT;
            }
        }

        public void MoveABLine(double dist)
        {
            double headingCalc;
            //calculate the heading 90 degrees to ref ABLine heading
            if (isABSameAsVehicleHeading)
            {
                headingCalc = abHeading + glm.PIBy2;
                moveDistance += dist;
            }
            else
            {
                headingCalc = abHeading - glm.PIBy2;
                moveDistance -= dist;
            }

            //calculate the new points for the reference line and points
            refPoint1.easting = (Math.Sin(headingCalc) * dist) + refPoint1.easting;
            refPoint1.northing = (Math.Cos(headingCalc) * dist) + refPoint1.northing;

            refABLineP1.easting = refPoint1.easting - (Math.Sin(abHeading) *   1600.0);
            refABLineP1.northing = refPoint1.northing - (Math.Cos(abHeading) * 1600.0);
                                                                                
            refABLineP2.easting = refPoint1.easting + (Math.Sin(abHeading) *   1600.0);
            refABLineP2.northing = refPoint1.northing + (Math.Cos(abHeading) * 1600.0);

            refPoint2.easting = refABLineP2.easting;
            refPoint2.northing = refABLineP2.northing;
        }

        public void DeleteAB()
        {
            refPoint1 = new vec2(0.0, 0.0);
            refPoint2 = new vec2(0.0, 1.0);

            refABLineP1 = new vec2(0.0, 0.0);
            refABLineP2 = new vec2(0.0, 1.0);

            currentABLineP1 = new vec2(0.0, 0.0);
            currentABLineP2 = new vec2(0.0, 1.0);

            abHeading = 0.0;
            passNumber = 0.0;
            howManyPathsAway = 0.0;
            isABLineSet = false;
            isABLineLoaded = false;
        }

        public void SetABLineByBPoint()
        {
            refPoint2.easting = mf.pn.fix.easting;
            refPoint2.northing = mf.pn.fix.northing;

            //calculate the AB Heading
            abHeading = Math.Atan2(refPoint2.easting - refPoint1.easting, refPoint2.northing - refPoint1.northing);
            if (abHeading < 0) abHeading += glm.twoPI;

            //sin x cos z for endpoints, opposite for additional lines
            refABLineP1.easting = refPoint1.easting - (Math.Sin(abHeading) *   1600.0);
            refABLineP1.northing = refPoint1.northing - (Math.Cos(abHeading) * 1600.0);

            refABLineP2.easting = refPoint1.easting + (Math.Sin(abHeading) *   1600.0);
            refABLineP2.northing = refPoint1.northing + (Math.Cos(abHeading) * 1600.0);

            isABLineSet = true;
            isABLineLoaded = true;
        }

        public void SetABLineByHeading()
        {
            //heading is set in the AB Form
            refABLineP1.easting = refPoint1.easting - (Math.Sin(abHeading) * 1600.0);
            refABLineP1.northing = refPoint1.northing - (Math.Cos(abHeading) * 1600.0);

            refABLineP2.easting = refPoint1.easting + (Math.Sin(abHeading) * 1600.0);
            refABLineP2.northing = refPoint1.northing + (Math.Cos(abHeading) * 1600.0);

            refPoint2.easting = refABLineP2.easting;
            refPoint2.northing = refABLineP2.northing;

            isABLineSet = true;
            isABLineLoaded = true;
        }

        public void SnapABLine()
        {
            double headingCalc;

            //calculate the heading 90 degrees to ref ABLine heading
            if (isOnRightSideCurrentLine)
            {
                headingCalc = abHeading + glm.PIBy2;
            }
            else
            {
                headingCalc = abHeading - glm.PIBy2;
            }

            if (isABSameAsVehicleHeading)
            {
                moveDistance += (distanceFromCurrentLine * 0.001);
            }
            else
            {
                moveDistance -= (distanceFromCurrentLine * 0.001);
            }


            //calculate the new points for the reference line and points
            refPoint1.easting = (Math.Sin(headingCalc) * Math.Abs(distanceFromCurrentLine) * 0.001) + refPoint1.easting;
            refPoint1.northing = (Math.Cos(headingCalc) * Math.Abs(distanceFromCurrentLine) * 0.001) + refPoint1.northing;

            refABLineP1.easting = refPoint1.easting - (Math.Sin(abHeading) *   1600.0);
            refABLineP1.northing = refPoint1.northing - (Math.Cos(abHeading) * 1600.0);

            refABLineP2.easting = refPoint1.easting + (Math.Sin(abHeading) *   1600.0);
            refABLineP2.northing = refPoint1.northing + (Math.Cos(abHeading) * 1600.0);

            refPoint2.easting = refABLineP2.easting;
            refPoint2.northing = refABLineP2.northing;
        }
    }

        public class CABLines
    {
        public vec2 ref1 = new vec2();
        public vec2 ref2 = new vec2();
        public vec2 origin = new vec2();
        public double heading = 0;
        public string Name = "aa";
    }

}