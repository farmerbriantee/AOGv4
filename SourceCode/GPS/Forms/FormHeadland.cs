﻿using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormHeadland : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf = null;

        private double maxFieldX, maxFieldY, minFieldX, minFieldY, fieldCenterX, fieldCenterY, maxFieldDistance;
        private Point fixPt;

        private bool isA, isSet;
        public double headWidth = 0;
        private int A, B, C, D, E, start = 99999, end = 99999;     

        public vec3 pint = new vec3(0.0, 1.0, 0.0);

        //list of coordinates of boundary line
        public List<vec3> headLineTemplate = new List<vec3>();

        private vec3[] hdx2;
        private vec3[] hdArr;

        public FormHeadland(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            InitializeComponent();
            //lblPick.Text = gStr.gsSelectALine;
            this.Text = gStr.gsHeadlandForm;
            btnReset.Text = gStr.gsResetAll;

            nudDistance.Controls[0].Enabled = false;
        }

        private void FormHeadland_Load(object sender, EventArgs e)
        {
            isA = true;
            isSet = false;

            //Builds line
            nudDistance.Value = (decimal)(Math.Round(mf.tool.toolWidth * 3,1));
            nudSetDistance.Value = 20;

            if (mf.hd.headArr[0].hdLine.Count > 0)
            {
                hdArr = new vec3[mf.hd.headArr[0].hdLine.Count];
                mf.hd.headArr[0].hdLine.CopyTo(hdArr);
                RebuildHeadLineTemplate();
            }
            else
            {
                BuildHeadLineTemplateFromBoundary();
            }
        }

        public void BuildHeadLineTemplateFromBoundary()
        {
            //to fill the list of line points
            vec3 point = new vec3();

            //outside boundary - count the points from the boundary
            headLineTemplate.Clear();

            int ptCount = mf.bnd.bndArr[0].bndLine.Count;
            for (int i = ptCount - 1; i >= 0; i--)
            {
                //calculate the point inside the boundary
                point.easting = mf.bnd.bndArr[0].bndLine[i].easting;
                point.northing = mf.bnd.bndArr[0].bndLine[i].northing;
                point.heading = mf.bnd.bndArr[0].bndLine[i].heading;
                headLineTemplate.Add(point);
            }

            start = end = 99999;
            isSet = false;

            int cnt = headLineTemplate.Count;

            hdArr = new vec3[cnt];
            headLineTemplate.CopyTo(hdArr);

            hdx2 = new vec3[cnt * 2];

            for (int i = 0; i < cnt; i++)
            {
                hdx2[i] = hdArr[i];
            }

            for (int i = cnt; i < cnt * 2; i++)
            {
                hdx2[i] = hdArr[i - cnt];
            }
        }

        private void RebuildHeadLineTemplate()
        {
            headLineTemplate.Clear();

            int cnt = hdArr.Length;
            for (int i = 0; i < cnt; i++)
            {
                vec3 pt = new vec3(hdArr[i]);
                headLineTemplate.Add(pt);
            }

            cnt = headLineTemplate.Count;

            hdArr = new vec3[cnt];
            headLineTemplate.CopyTo(hdArr);

            hdx2 = new vec3[cnt * 2];

            for (int i = 0; i < cnt; i++)
            {
                hdx2[i] = hdArr[i];
            }

            for (int i = cnt; i < cnt * 2; i++)
            {
                hdx2[i] = hdArr[i - cnt];
            }

            start = end = 99999;
            isSet = false;
        }

        private void FixTurnLine(double totalHeadWidth, List<vec3> curBnd, double spacing)
        {
            //count the points from the boundary

            var foos = new List<vec3>(hdArr);

            int lineCount = foos.Count;
            double distance;

            //int headCount = mf.bndArr[inTurnNum].bndLine.Count;
            int bndCount = curBnd.Count;

            //remove the points too close to boundary
            for (int i = 0; i < bndCount; i++)
            {
                for (int j = 0; j < lineCount; j++)
                {
                    //make sure distance between headland and boundary is not less then width
                    distance = glm.Distance(curBnd[i], foos[j]);
                    if (distance < (totalHeadWidth * 0.96))
                    {
                        foos.RemoveAt(j);
                        lineCount = foos.Count;
                        j = -1;
                    }
                }
            }

            //make sure distance isn't too small between points on turnLine
            bndCount = foos.Count;

            //double spacing = mf.tool.toolWidth * 0.25;
            for (int i = 0; i < bndCount - 1; i++)
            {
                distance = glm.Distance(foos[i], foos[i + 1]);
                if (distance < spacing)
                {
                    foos.RemoveAt(i + 1);
                    bndCount = foos.Count;
                    i--;
                }
            }

            bndCount = foos.Count;

            hdArr = new vec3[bndCount];
            foos.CopyTo(hdArr);

            ////make sure distance isn't too big between points on Turn
            //bndCount = foos.Count;
            //for (int i = 0; i < bndCount; i++)
            //{
            //    int j = i + 1;
            //    if (j == bndCount) j = 0;
            //    distance = glm.DistanceSquared(foos[i].easting, foos[i].northing, foos[j].easting, foos[j].northing);
            //    if (distance > 2.3)
            //    {
            //        vec3 pointB = new vec3((foos[i].easting + foos[j].easting) / 2.0,
            //            (foos[i].northing + foos[j].northing) / 2.0,
            //            foos[j].heading);

            //        foos.Insert(j, pointB);
            //        bndCount = foos.Count;
            //        i--;
            //    }
            //}
        }

        private void btnSetDistance_Click(object sender, EventArgs e)
        {
            if (end > headLineTemplate.Count)
            {
                for (int i = start; i < hdArr.Length; i++)
                {
                    hdArr[i].easting = headLineTemplate[i].easting
                        + (-Math.Sin(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudSetDistance.Value);
                    hdArr[i].northing = headLineTemplate[i].northing
                        + (-Math.Cos(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudSetDistance.Value);
                    hdArr[i].heading = headLineTemplate[i].heading;
                }

                end -= hdArr.Length;
                for (int i = 0; i < end; i++)
                {
                    hdArr[i].easting = headLineTemplate[i].easting
                        + (-Math.Sin(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudSetDistance.Value);
                    hdArr[i].northing = headLineTemplate[i].northing
                        + (-Math.Cos(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudSetDistance.Value);
                    hdArr[i].heading = headLineTemplate[i].heading;
                }
            }
            else
            {
                for (int i = start; i < end; i++)
                {
                    //calculate the point inside the boundary
                    hdArr[i].easting = headLineTemplate[i].easting
                        + (-Math.Sin(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudSetDistance.Value);
                    hdArr[i].northing = headLineTemplate[i].northing
                        + (-Math.Cos(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudSetDistance.Value);
                    hdArr[i].heading = headLineTemplate[i].heading;
                }
            }

            isSet = false;
            start = 99999;
            end = 99999;
            RebuildHeadLineTemplate();
        }
        
        private void btnMakeFixedHeadland_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < headLineTemplate.Count; i++)
            {
                //calculate the point inside the boundary
                hdArr[i].easting = headLineTemplate[i].easting + (-Math.Sin(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudDistance.Value);
                hdArr[i].northing = headLineTemplate[i].northing + (-Math.Cos(glm.PIBy2 + headLineTemplate[i].heading) * (double)nudDistance.Value);
                hdArr[i].heading = headLineTemplate[i].heading;
            }

            FixTurnLine((double)nudDistance.Value, headLineTemplate, 2);

            isSet = false;
            start = 99999;
            end = 99999;
            RebuildHeadLineTemplate();
        }

        private void oglSelf_Paint(object sender, PaintEventArgs e)
        {
            oglSelf.MakeCurrent();

            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            GL.LoadIdentity();                  // Reset The View

            CalculateMinMax();

            //back the camera up
            GL.Translate(0, 0, -maxFieldDistance);

            //translate to that spot in the world
            GL.Translate(-fieldCenterX, -fieldCenterY, 0);

            GL.Color3(1, 1, 1);

            if (start == 99999)
                lblStart.Text = "--";
            else lblStart.Text = start.ToString();

            if (end == 99999)
                 lblEnd.Text = "--";
            else lblEnd.Text = end.ToString();

            //draw all the boundaries
            mf.bnd.DrawBoundaryLines();

            int ptCount = hdArr.Length;
            if (ptCount > 1)
            {
                GL.LineWidth(1);
                GL.Color3(0.20f, 0.96232f, 0.30f);
                GL.PointSize(2);
                GL.Begin(PrimitiveType.LineStrip);
                for (int h = 0; h < ptCount; h++) GL.Vertex3(hdArr[h].easting, hdArr[h].northing, 0);

                GL.Color3(0.60f, 0.9232f, 0.0f);
                GL.Vertex3(hdArr[0].easting, hdArr[0].northing, 0);
                GL.End();
            }

            GL.PointSize(8.0f);
            GL.Begin(PrimitiveType.Points);
            GL.Color3(0.95f, 0.90f, 0.0f);
            GL.Vertex3(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0.0);
            GL.End();

            DrawABTouchLine();

            GL.Flush();
            oglSelf.SwapBuffers();
        }

        private void oglSelf_MouseDown(object sender, MouseEventArgs e)
        {
            if (isSet) return;

            Point pt = oglSelf.PointToClient(Cursor.Position);

            //Convert to Origin in the center of window, 800 pixels
            fixPt.X = pt.X - 350;
            fixPt.Y = (700 - pt.Y - 350);
            vec3 plotPt = new vec3
            {
                //convert screen coordinates to field coordinates
                easting = ((double)fixPt.X) * (double)maxFieldDistance / 632.0,
                northing = ((double)fixPt.Y) * (double)maxFieldDistance / 632.0,
                heading = 0
            };

            plotPt.easting += fieldCenterX;
            plotPt.northing += fieldCenterY;

            pint.easting = plotPt.easting;
            pint.northing = plotPt.northing;

            if (isA)
            {
                double minDistA = 1000000, minDistB = 1000000;
                start = 99999; end = 99999;

                int ptCount = hdx2.Length;

                if (ptCount > 0)
                {
                    //find the closest 2 points to current fix
                    for (int t = 0; t < ptCount; t++)
                    {
                        double dist = ((pint.easting - hdx2[t].easting) * (pint.easting - hdx2[t].easting))
                                        + ((pint.northing - hdx2[t].northing) * (pint.northing - hdx2[t].northing));
                        if (dist < minDistA)
                        {
                            minDistB = minDistA;
                            B = A;
                            minDistA = dist;
                            A = t;
                        }
                        else if (dist < minDistB)
                        {
                            minDistB = dist;
                            B = t;
                        }
                    }

                    //just need to make sure the points continue ascending or heading switches all over the place
                    if (A > B) { E = A; A = B; B = E; }

                    start = A;
                }

                isA = false;
            }
            else
            {
                double minDistA = 1000000, minDistB = 1000000;

                int ptCount = hdx2.Length;

                if (ptCount > 0)
                {
                    //find the closest 2 points to current point
                    for (int t = 0; t < ptCount; t++)
                    {
                        double dist = ((pint.easting - hdx2[t].easting) * (pint.easting - hdx2[t].easting))
                                        + ((pint.northing - hdx2[t].northing) * (pint.northing - hdx2[t].northing));
                        if (dist < minDistA)
                        {
                            minDistB = minDistA;
                            D = C;
                            minDistA = dist;
                            C = t;
                        }
                        else if (dist < minDistB)
                        {
                            minDistB = dist;
                            D = t;
                        }
                    }

                    //just need to make sure the points continue ascending or heading switches all over the place
                    if (C > D) { E = C; C = D; D = E; }
                }

                isA = true;

                int A1 = Math.Abs(A - C);
                int B1 = Math.Abs(A - D);
                int C1 = Math.Abs(B - C);
                int D1 = Math.Abs(B - D);

                if (A1 <= B1 && A1 <= C1 && A1 <= D1) { start = A; end = C; }
                else if (B1 <= A1 && B1 <= C1 && B1 <= D1) { start = A; end = D; }
                else if (C1 <= B1 && C1 <= A1 && C1 <= D1) { start = B; end = C; }
                else if (D1 <= B1 && D1 <= C1 && D1 <= A1) { start = B; end = D; }

                if (start > end) { E = start; start = end; end = E; }

                isSet = true;
            }
        }

        private void btnMoveUp_Click_1(object sender, EventArgs e)
        {
            if (end > hdArr.Length)
            {
                for (int i = start; i < hdArr.Length; i++)
                    hdArr[i].northing += 1;

                int endAdj = end - hdArr.Length;
                for (int i = 0; i < endAdj; i++)
                    hdArr[i].northing += 1;
            }
            else
            {
                for (int i = start; i < end; i++)
                    hdArr[i].northing += 1;
            }

        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            if (end > hdArr.Length)
            {
                for (int i = start; i < hdArr.Length; i++)
                    hdArr[i].northing -= 1;

                int endAdj = end - hdArr.Length;
                for (int i = 0; i < endAdj; i++)
                    hdArr[i].northing -= 1;
            }
            else
            {
                for (int i = start; i < end; i++)
                    hdArr[i].northing -= 1;
            }
        }

        private void btnMoveLeft_Click(object sender, EventArgs e)
        {
            if (end > hdArr.Length)
            {
                for (int i = start; i < hdArr.Length; i++)
                    hdArr[i].easting -= 1;

                int endAdj = end - hdArr.Length;
                for (int i = 0; i < endAdj; i++)
                    hdArr[i].easting -= 1;
            }
            else
            {
                for (int i = start; i < end; i++)
                    hdArr[i].easting -= 1;
            }
        }

        private void btnMoveRight_Click(object sender, EventArgs e)
        {
            if (end > hdArr.Length)
            {
                for (int i = start; i < hdArr.Length; i++)
                    hdArr[i].easting += 1;

                int endAdj = end - hdArr.Length;
                for (int i = 0; i < endAdj; i++)
                    hdArr[i].easting += 1;
            }
            else
            {
                for (int i = start; i < end; i++)
                    hdArr[i].easting += 1;
            }
        }

        private void btnDoneManualMove_Click(object sender, EventArgs e)
        {
            RebuildHeadLineTemplate();
        }

        private void DrawABTouchLine()
        {
            GL.PointSize(6);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0.990, 0.00, 0.250);
            if (start != 99999) GL.Vertex3(hdx2[start].easting, hdx2[start].northing, 0);

            GL.Color3(0.990, 0.960, 0.250);
            if (end != 99999) GL.Vertex3(hdx2[end].easting, hdx2[end].northing, 0);
            GL.End();

            if (start != 99999 && end != 99999)
            {
                GL.Color3(0.965, 0.250, 0.950);
                //draw the turn line oject
                GL.LineWidth(2.0f);
                GL.Begin(PrimitiveType.LineStrip);
                int ptCount = hdx2.Length;
                if (ptCount < 1) return;
                for (int c = start; c < end; c++) GL.Vertex3(hdx2[c].easting, hdx2[c].northing, 0);

                GL.End();
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            BuildHeadLineTemplateFromBoundary();
        }

        private void nudDistance_Enter(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender);
            btnExit.Focus();
        }

        private void nudSetDistance_Enter(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender);
            btnExit.Focus();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            oglSelf.Refresh();
            if (isSet)
            {
                btnExit.Enabled = false;
                btnMakeFixedHeadland.Enabled = false;
                nudDistance.Enabled = false;

                nudSetDistance.Enabled = true;
                btnSetDistance.Enabled = true;
                btnMoveLeft.Enabled = true;
                btnMoveRight.Enabled = true;
                btnMoveUp.Enabled = true;
                btnMoveDown.Enabled = true;
                btnDoneManualMove.Enabled = true;
                btnDeletePoints.Enabled = true;
                btnStartUp.Enabled = true;
                btnStartDown.Enabled = true;
                btnEndDown.Enabled = true;
                btnEndUp.Enabled = true;
            }
            else
            {
                nudSetDistance.Enabled = false;
                btnSetDistance.Enabled = false;
                btnMoveLeft.Enabled = false;
                btnMoveRight.Enabled = false;
                btnMoveUp.Enabled = false;
                btnMoveDown.Enabled = false;
                btnDoneManualMove.Enabled = false;
                btnDeletePoints.Enabled = false;
                btnStartUp.Enabled = false;
                btnStartDown.Enabled = false;
                btnEndDown.Enabled = false;
                btnEndUp.Enabled = false;

                btnExit.Enabled = true;
                btnMakeFixedHeadland.Enabled = true;
                nudDistance.Enabled = true;
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            mf.hd.headArr[0].hdLine?.Clear();
            mf.hd.headArr[0].isDrawList?.Clear();

            double delta = 0;
            for (int i = 0; i < hdArr.Length; i++)
            {
                if (i == 0)
                {
                    mf.hd.headArr[0].hdLine.Add(new vec3(hdArr[i].easting, hdArr[i].northing, hdArr[i].heading));
                    mf.hd.headArr[0].isDrawList.Add(true);
                    continue;
                }
                delta += (hdArr[i - 1].heading - hdArr[i].heading);

                if (Math.Abs(delta) > 0.1)
                {
                    vec3 pt = new vec3(hdArr[i].easting, hdArr[i].northing, hdArr[i].heading);

                    mf.hd.headArr[0].hdLine.Add(pt);
                    delta = 0;
                    if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(pt)) mf.hd.headArr[0].isDrawList.Add(true);
                    else mf.hd.headArr[0].isDrawList.Add(false);
                }
            }



            //for (int i = 0; i < hdArr.Length; i++)
            //{
            //    vec3 pt = new vec3(hdArr[i].easting, hdArr[i].northing, hdArr[i].heading);
            //    mf.hd.headArr[0].hdLine.Add(pt);

            //    if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(pt)) mf.hd.headArr[0].isDrawList.Add(true);
            //    else mf.hd.headArr[0].isDrawList.Add(false);
            //}

            mf.hd.headArr[0].PreCalcHeadLines();

            mf.FileSaveHeadland();

            Close();
        }

        private void btnTurnOffHeadland_Click(object sender, EventArgs e)
        {
            mf.hd.headArr[0].hdLine?.Clear();

            mf.hd.headArr[0].calcList?.Clear();

            mf.hd.headArr[0].isDrawList?.Clear();

            mf.FileSaveHeadland();

            Close();
        }

        private void btnDeletePoints_Click(object sender, EventArgs e)
        {
            if (end > hdArr.Length)
            {
                headLineTemplate.RemoveRange(start, headLineTemplate.Count - start);

                int endAdj = end - hdArr.Length;
                headLineTemplate.RemoveRange(0, endAdj);
            }
            else
            {
                headLineTemplate.RemoveRange(start, end - start);
            }

            int bndCount = headLineTemplate.Count;
            for (int i = 0; i < bndCount; i++)
            {
                int j = i + 1;
                if (j == bndCount) j = 0;
                double distanceSq = glm.DistanceSquared(headLineTemplate[i].easting, headLineTemplate[i].northing, 
                                                headLineTemplate[j].easting, headLineTemplate[j].northing);
                if (distanceSq > 2.3)
                {
                    vec3 pointB = new vec3((headLineTemplate[i].easting + headLineTemplate[j].easting) / 2.0,
                        (headLineTemplate[i].northing + headLineTemplate[j].northing) / 2.0,
                        headLineTemplate[j].heading);

                    headLineTemplate.Insert(j, pointB);
                    bndCount = headLineTemplate.Count;
                    i--;
                }
            }

            int cnt = headLineTemplate.Count;
            hdArr = new vec3[cnt];
            headLineTemplate.CopyTo(hdArr);

            RebuildHeadLineTemplate();
        }


        private void btnStartDown_MouseDown(object sender, MouseEventArgs e)
        {
            if (start != 99999)
            {
                if (start > 0 && start < end-1) start--;
            }
        }

        private void btnStartUp_MouseDown(object sender, MouseEventArgs e)
        {
            if (start != 99999)
            {
                if (start < end-2) start++;
            }
        }

        private void btnEndDown_MouseDown(object sender, MouseEventArgs e)
        {
            if (end != 99999)
            {
                if (end-2 > start) end--;
            }
        }

        private void btnEndUp_MouseDown(object sender, MouseEventArgs e)
        {
            if (end != 99999) 
            {
                if (end-1 > start && end < hdx2.Length) end++;
            }
        }

        private void oglSelf_Load(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.ClearColor(0.23122f, 0.2318f, 0.2315f, 1.0f);
        }

        private void oglSelf_Resize(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            //58 degrees view
            Matrix4 mat = Matrix4.CreatePerspectiveFieldOfView(1.01f, 1.0f, 1.0f, 20000);
            GL.LoadMatrix(ref mat);

            GL.MatrixMode(MatrixMode.Modelview);
        }

        //determine mins maxs of patches and whole field.
        private void CalculateMinMax()
        {
            minFieldX = 9999999; minFieldY = 9999999;
            maxFieldX = -9999999; maxFieldY = -9999999;

            //draw patches j= # of sections
            for (int j = 0; j < mf.tool.numSuperSection; j++)
            {
                //every time the section turns off and on is a new patch
                int patchCount = mf.section[j].patchList.Count;

                if (patchCount > 0)
                {
                    //for every new chunk of patch
                    foreach (var triList in mf.section[j].patchList)
                    {
                        int count2 = triList.Count;
                        for (int i = 0; i < count2; i += 3)
                        {
                            double x = triList[i].easting;
                            double y = triList[i].northing;

                            //also tally the max/min of field x and z
                            if (minFieldX > x) minFieldX = x;
                            if (maxFieldX < x) maxFieldX = x;
                            if (minFieldY > y) minFieldY = y;
                            if (maxFieldY < y) maxFieldY = y;
                        }
                    }
                }

                //min max of the boundary
                if (mf.bnd.bndArr.Count > 0)
                {
                    int bndCnt = mf.bnd.bndArr[0].bndLine.Count;
                    for (int i = 0; i < bndCnt; i++)
                    {
                        double x = mf.bnd.bndArr[0].bndLine[i].easting;
                        double y = mf.bnd.bndArr[0].bndLine[i].northing;

                        //also tally the max/min of field x and z
                        if (minFieldX > x) minFieldX = x;
                        if (maxFieldX < x) maxFieldX = x;
                        if (minFieldY > y) minFieldY = y;
                        if (maxFieldY < y) maxFieldY = y;
                    }
                }

                if (maxFieldX == -9999999 || minFieldX == 9999999 || maxFieldY == -9999999 || minFieldY == 9999999)
                {
                    maxFieldX = 0; minFieldX = 0; maxFieldY = 0; minFieldY = 0;
                }
                else
                {
                    //the largest distancew across field
                    double dist = Math.Abs(minFieldX - maxFieldX);
                    double dist2 = Math.Abs(minFieldY - maxFieldY);

                    if (dist > dist2) maxFieldDistance = dist;
                    else maxFieldDistance = dist2;

                    if (maxFieldDistance < 100) maxFieldDistance = 100;
                    if (maxFieldDistance > 19900) maxFieldDistance = 19900;
                    //lblMax.Text = ((int)maxFieldDistance).ToString();

                    fieldCenterX = (maxFieldX + minFieldX) / 2.0;
                    fieldCenterY = (maxFieldY + minFieldY) / 2.0;
                }

                //if (isMetric)
                //{
                //    lblFieldWidthEastWest.Text = Math.Abs((maxFieldX - minFieldX)).ToString("N0") + " m";
                //    lblFieldWidthNorthSouth.Text = Math.Abs((maxFieldY - minFieldY)).ToString("N0") + " m";
                //}
                //else
                //{
                //    lblFieldWidthEastWest.Text = Math.Abs((maxFieldX - minFieldX) * glm.m2ft).ToString("N0") + " ft";
                //    lblFieldWidthNorthSouth.Text = Math.Abs((maxFieldY - minFieldY) * glm.m2ft).ToString("N0") + " ft";
                //}
            }
        }
    }
}
