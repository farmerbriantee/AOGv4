﻿//Please, if you use this, share the improvements

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Globalization;
using OpenTK;
using OpenTK.Graphics.OpenGL;


namespace AgOpenGPS
{
    public partial class FormGPS
    {
        //ABLines directory
        public string ablinesDirectory;

        //colors for sections and field background
        public byte flagColor = 0;

        //how many cm off line per big pixel
        public int lightbarCmPerPixel;

        //polygon mode for section drawing
        public bool isDrawPolygons;

        //Is it in 2D or 3D, metric or imperial, display lightbar, display grid etc
        public bool isMetric = true, isLightbarOn = true, isGridOn, isFullScreen;
        public bool isUTurnAlwaysOn, isCompassOn, isSpeedoOn, isAutoDayNight, isSideGuideLines = true;
        public bool isPureDisplayOn = true, isSkyOn = true, isRollMeterOn = false;
        public bool isDay = true, isDayTime = true, isSimple;
        public bool isKeyboardOn = true;

        //master Manual and Auto, 3 states possible
        public enum btnStates { Off, Auto, On }
        public btnStates manualBtnState = btnStates.Off;
        public btnStates autoBtnState = btnStates.Off;

        public Color dayColor = Properties.Settings.Default.setDisplay_colorDayMode;
        public Color nightColor = Properties.Settings.Default.setDisplay_colorNightMode;
        public Color sectionColorDay = Properties.Settings.Default.setDisplay_colorSectionsDay;
        public Color sectionColorNight = Properties.Settings.Default.setDisplay_colorSectionsNight;
        public Color fieldColorDay = Properties.Settings.Default.setDisplay_colorFieldDay;
        public Color fieldColorNight = Properties.Settings.Default.setDisplay_colorFieldNight;

        public int[] customColorsList = new int[16];

        //sunrise sunset
        public DateTime dateToday = DateTime.Today;
        public DateTime sunrise = DateTime.Now;
        public DateTime sunset = DateTime.Now;

        public uint sentenceCounter = 0;

        //section button states
        public enum manBtn { Off, Auto, On }

        //Timer triggers at 10 msec, and is THE clock of the whole program
        //Timer stopped while parsing nmea
        private void tmrWatchdog_tick(object sender, EventArgs e)
        {
            //Check for a newline char, if none then just return
            sentenceCounter++;
            int cr = pn.rawBuffer.IndexOf("\n", StringComparison.Ordinal);
            if (sentenceCounter > 80) 
                ShowNoGPSWarning();

            if (cr == -1) return;

            //go see if data ready for draw and position updates
            tmrWatchdog.Enabled = false;

            //did we get a new fix position?
            if (ScanForNMEA())
            {
                //reset the dead GPS counter
                if (sentenceCounter > 98)
                {
                    camera.camSetDistance = -200;
                    SetZoom();
                    //GL.ClearColor(0.5122f, 0.58f, 0.75f, 1.0f);
                }

                sentenceCounter = 0;

                if (threeSecondCounter++ >= fixUpdateHz * 2)
                {
                    threeSecondCounter = 0;
                    threeSeconds++;
                }
                if (oneSecondCounter++ >= fixUpdateHz)
                {
                    oneSecondCounter = 0;
                    oneSecond++;
                }
                if (oneHalfSecondCounter++ >= fixUpdateHz / 2)
                {
                    oneHalfSecondCounter = 0;
                    oneHalfSecond++;
                }
                if (oneFifthSecondCounter++ >= fixUpdateHz / 5)
                {
                    oneFifthSecondCounter = 0;
                    oneFifthSecond++;
                }
                
                /////////////////////////////////////////////////////////   333333333333333  ////////////////////////////////////////
                //every 3 second update status
                if (displayUpdateThreeSecondCounter != threeSeconds)
                {
                    //reset the counter
                    displayUpdateThreeSecondCounter = threeSeconds;

                    //check to make sure the grid is big enough
                    worldGrid.checkZoomWorldGrid(pn.fix.northing, pn.fix.easting);

                    if (isMetric)
                    {
                        //lblTotalFieldArea.Text = fd.AreaBoundaryLessInnersHectares;
                        //lblTotalAppliedArea.Text = fd.WorkedHectares;
                        //lblWorkRemaining.Text = fd.WorkedAreaRemainHectares;
                        //lblPercentRemaining.Text = fd.WorkedAreaRemainPercentage;
                        //lblTimeRemaining.Text = fd.TimeTillFinished;

                        fieldStatusStripText.Text = fd.WorkedAreaRemainHectares + "\r\n"+
                                                       fd.WorkedAreaRemainPercentage +"\r\n" +
                                                       fd.TimeTillFinished + "\r\n" +
                                                       fd.WorkRateHectares;


                        //lblAreaAppliedMinusOverlap.Text = ((fd.actualAreaCovered * glm.m2ha).ToString("N2"));
                        //lblAreaMinusActualApplied.Text = (((fd.areaBoundaryOuterLessInner - fd.actualAreaCovered) * glm.m2ha).ToString("N2"));
                        //lblOverlapPercent.Text = (fd.overlapPercent.ToString("N2")) + "%";
                        //lblAreaOverlapped.Text = (((fd.workedAreaTotal - fd.actualAreaCovered) * glm.m2ha).ToString("N3"));

                        //btnManualOffOn.Text = fd.AreaBoundaryLessInnersHectares;
                        //lblEqSpec.Text = (Math.Round(tool.toolWidth, 2)).ToString() + " m  " + vehicleFileName + toolFileName;
                    }
                    else //imperial
                    {
                        //lblTotalFieldArea.Text = fd.AreaBoundaryLessInnersAcres;
                        //lblTotalAppliedArea.Text = fd.WorkedAcres;
                        //lblWorkRemaining.Text = fd.WorkedAreaRemainAcres;
                        //lblPercentRemaining.Text = fd.WorkedAreaRemainPercentage;
                        //lblTimeRemaining.Text = fd.TimeTillFinished;

                        //lblAreaAppliedMinusOverlap.Text = ((fd.actualAreaCovered * glm.m2ac).ToString("N2"));
                        //lblAreaMinusActualApplied.Text = (((fd.areaBoundaryOuterLessInner - fd.actualAreaCovered) * glm.m2ac).ToString("N2"));
                        //lblOverlapPercent.Text = (fd.overlapPercent.ToString("N2")) + "%";
                        //lblAreaOverlapped.Text = (((fd.workedAreaTotal - fd.actualAreaCovered) * glm.m2ac).ToString("N3"));

                        fieldStatusStripText.Text = fd.WorkedAreaRemainAcres + "\r\n" +
                               fd.WorkedAreaRemainPercentage + "\r\n" +
                               fd.TimeTillFinished + "\r\n" +
                               fd.WorkRateAcres;

                        //btnManualOffOn.Text = fd.AreaBoundaryLessInnersAcres;
                        //lblEqSpec.Text =  (Math.Round(tool.toolWidth * glm.m2ft, 2)).ToString() + " ft  " + vehicleFileName + toolFileName;
                    }

                    //not Metric/Standard units sensitive
                    if (ABLine.isBtnABLineOn) btnABLine.Text = "# " + PassNumber;
                    else btnABLine.Text = "";

                    if (curve.isBtnCurveOn) btnCurve.Text = "# " + CurveNumber;
                    else btnCurve.Text = "";

                    lblDateTime.Text = DateTime.Now.ToString("HH:mm:ss") + "\n\r" + DateTime.Now.ToString("ddd, MMMM dd, yyyy");
                }//end every 3 seconds

                //every second update all status ///////////////////////////   1 1 1 1 1 1 ////////////////////////////
                if (displayUpdateOneSecondCounter != oneSecond)
                {
                    //reset the counter
                    displayUpdateOneSecondCounter = oneSecond;

                    //counter used for saving field in background
                    minuteCounter++;
                    tenMinuteCounter++;

                    //if (isRTK)
                    //{
                    //    if (pn.fixQuality == 4) lblHz.BackColor = Color.Transparent;
                    //    else lblHz.BackColor = Color.Salmon;
                    //}
                    //else lblHz.BackColor = Color.Transparent;

                    if (ABLine.isBtnABLineOn && !ct.isContourBtnOn)
                    {
                        btnEditHeadingB.Text = ((int)(ABLine.moveDistance * 100)).ToString();
                    }
                    if (curve.isBtnCurveOn && !ct.isContourBtnOn)
                    {
                        btnEditHeadingB.Text = ((int)(curve.moveDistance * 100)).ToString();
                    }

                    //pbarAutoSteerComm.Value = pbarSteer;
                    //pbarUDPComm.Value = pbarUDP;
                    //pbarMachineComm.Value = pbarMachine;

                    //if (mc.steerSwitchValue == 0)
                    //{
                    //    this.btnAutoSteer.BackColor = System.Drawing.Color.SkyBlue;
                    //}
                    //else
                    //{
                    //    this.btnAutoSteer.BackColor = System.Drawing.Color.Transparent;
                    //}
                    
                    //AutoSteerAuto button enable - Ray Bear inspired code - Thx Ray!
                    if (isJobStarted && ahrs.isAutoSteerAuto && 
                        (ABLine.isBtnABLineOn || ct.isContourBtnOn || curve.isBtnCurveOn))
                    {
                        if (mc.steerSwitchValue == 0)
                        {
                            if (!isAutoSteerBtnOn) btnAutoSteer.PerformClick();
                        }
                        else
                        {
                            if ( isAutoSteerBtnOn) btnAutoSteer.PerformClick();
                        }
                    }

                    //Make sure it is off when it should
                    if ((!ABLine.isBtnABLineOn && !ct.isContourBtnOn && !curve.isBtnCurveOn && isAutoSteerBtnOn) 
                        ) btnAutoSteer.PerformClick();

                    //do all the NTRIP routines
                    DoNTRIPSecondRoutine();

                    //the main formgps window
                    if (isMetric)  //metric or imperial
                    {
                        //Hectares on the master section soft control and sections
                        btnSectionOffAutoOn.Text = fd.WorkedHectares;

                        //status strip values
                        distanceToolBtn.Text = fd.DistanceUserMeters + "\r\n" + fd.WorkedUserHectares2;

                    }
                    else  //Imperial Measurements
                    {
                        //acres on the master section soft control and sections
                        btnSectionOffAutoOn.Text = fd.WorkedAcres;

                        //status strip values
                        distanceToolBtn.Text = fd.DistanceUserFeet + "\r\n" + fd.WorkedUserAcres2;
                    }

                    //statusbar flash red undefined headland
                    if (mc.isOutOfBounds && statusStripBottom.BackColor == Color.Transparent
                        || !mc.isOutOfBounds && statusStripBottom.BackColor == Color.Tomato)
                    {
                        if (!mc.isOutOfBounds)
                        {
                            statusStripBottom.BackColor = Color.Transparent;
                        }
                        else
                        {
                            statusStripBottom.BackColor = Color.Tomato;
                        }
                    }
                }

                //every half of a second update all status  ////////////////    0.5  0.5   0.5    0.5    /////////////////
                if (displayUpdateHalfSecondCounter != oneHalfSecond)
                {
                    //reset the counter
                    displayUpdateHalfSecondCounter = oneHalfSecond;

                    //the main formgps window
                    if (isMetric)  //metric or imperial
                    {
                        lblSpeed.Text = SpeedKPH;
                        btnContour.Text = XTE; //cross track error

                    }
                    else  //Imperial Measurements
                    {
                        lblSpeed.Text = SpeedMPH;
                        btnContour.Text = InchXTE; //cross track error
                    }

                    lblHz.Text = fixUpdateHz + "Hz  ms:" + (frameTime.ToString("N1")) + "\r\n" + 
                        FixQuality;

                    AutoSteerToolBtn.Text = SetSteerAngle + "\r\n" + ActualSteerAngle;
                } //end every 1/2 second

                //every fifth second update  ///////////////////////////   FIFTH Fifth ////////////////////////////
                if (displayUpdateOneFifthCounter != oneFifthSecond)
                {
                    //reset the counter
                    displayUpdateOneFifthCounter = oneFifthSecond;
                }

            } //there was a new GPS update

            //start timer again and wait for new fix
            tmrWatchdog.Enabled = true;

        }//wait till timer fires again.  

        private void IsBetweenSunriseSunset(double lat, double lon)
        {
            CSunTimes.Instance.CalculateSunRiseSetTimes(pn.latitude, pn.longitude, dateToday, ref sunrise, ref sunset);
            isDay = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);
        }

        private void LoadGUI()
        {
            isSkyOn = Settings.Default.setMenu_isSkyOn;
            isGridOn = Settings.Default.setMenu_isGridOn;
            isCompassOn = Settings.Default.setMenu_isCompassOn;
            isSpeedoOn = Settings.Default.setMenu_isSpeedoOn;
            isAutoDayNight = Settings.Default.setDisplay_isAutoDayNight;
            isSideGuideLines = Settings.Default.setMenu_isSideGuideLines;
            isLogNMEA = Settings.Default.setMenu_isLogNMEA;
            isPureDisplayOn = Settings.Default.setMenu_isPureOn;
            isUTurnAlwaysOn = Settings.Default.setMenu_isUTurnAlwaysOn;                                         

            //set the language to last used
            SetLanguage(Settings.Default.setF_culture);

            currentVersionStr = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);

            string[] fullVers = currentVersionStr.Split('.');
            int inoV = int.Parse(fullVers[0], CultureInfo.InvariantCulture);
            inoV += int.Parse(fullVers[1], CultureInfo.InvariantCulture);
            inoV += int.Parse(fullVers[2], CultureInfo.InvariantCulture);
            inoVersionInt = inoV;
            inoVersionStr = inoV.ToString();

            simulatorOnToolStripMenuItem.Checked = Settings.Default.setMenu_isSimulatorOn;
            if (simulatorOnToolStripMenuItem.Checked)
            {
                panelSim.Visible = true;
                timerSim.Enabled = true;
            }
            else
            {
                panelSim.Visible = false;
                timerSim.Enabled = false;
            }

            fixUpdateHz = Properties.Settings.Default.setPort_NMEAHz;
            if (timerSim.Enabled) fixUpdateHz = 10;
            fixUpdateTime = 1 / (double)fixUpdateHz;

            //set the flag mark button to red dot
            btnFlag.Image = Properties.Resources.FlagRed;

            //night mode
            //isDay = Properties.Settings.Default.setDisplay_isDayMode;
            isDay = !isDay;
            SwapDayNightMode();

            //load the string of custom colors
            string[] words = Properties.Settings.Default.setDisplay_customColors.Split(',');
            for (int i = 0; i < 16; i++)
            {
                customColorsList[i] = int.Parse(words[i], CultureInfo.InvariantCulture);
            }

            isSimple = Properties.Settings.Default.setDisplay_isSimple;

            //metric settings
            isMetric = Settings.Default.setMenu_isMetric;

            //load up colors
            fieldColorDay = (Settings.Default.setDisplay_colorFieldDay);
            sectionColorDay = (Settings.Default.setDisplay_colorSectionsDay);
            fieldColorNight = (Settings.Default.setDisplay_colorFieldNight);
            sectionColorNight = (Settings.Default.setDisplay_colorSectionsNight);

            DisableYouTurnButtons();

            isLightbarOn = Settings.Default.setMenu_isLightbarOn;
            lightbarToolStripMenuItem.Checked = isLightbarOn;

            //set up grid and lightbar

            isKeyboardOn = Settings.Default.setDisplay_isKeyboardOn;
            keyboardToolStripMenuItem1.Checked = isKeyboardOn;


            if (Settings.Default.setMenu_isOGLZoomOn == 1)
                topFieldViewToolStripMenuItem.Checked = true;
            else topFieldViewToolStripMenuItem.Checked = false;

            oglZoom.Width = 400;
            oglZoom.Height = 400;
            oglZoom.Visible = true;
            oglZoom.Left = 300;
            oglZoom.Top = 80;

            oglZoom.SendToBack();


            LineUpManualBtns();

            yt.rowSkipsWidth = Properties.Vehicle.Default.set_youSkipWidth;
            cboxpRowWidth.SelectedIndex = yt.rowSkipsWidth - 1;

            //default to come up in mini panel, exit remembers 

            SwapBatmanPanels();

            if (Properties.Settings.Default.setAS_isAutoSteerAutoOn) btnAutoSteer.Text = "R";
            else btnAutoSteer.Text = "M";

            //panelSim.Location = Settings.Default.setDisplay_panelSimLocation;

            FixPanelsAndMenus();

            layoutPanelRight.Enabled = false;
            //boundaryToolStripBtn.Enabled = false;
            toolStripBtnDropDownBoundaryTools.Enabled = false;

            if (isNTRIP_RequiredOn)
            {
                //btnStartStopNtrip.Visible = true;
                NTRIPStartStopStrip.Visible = true;
                lblWatch.Visible = true;
                NTRIPBytesMenu.Visible = true;
                pbarNtripMenu.Visible = true;
            }
            else
            {
                //btnStartStopNtrip.Visible = false;
                NTRIPStartStopStrip.Visible = false;
                lblWatch.Visible = false;
                NTRIPBytesMenu.Visible = false;
                pbarNtripMenu.Visible = false;
            }

            if (hd.isOn) btnHeadlandOnOff.Image = Properties.Resources.HeadlandOn;
            else btnHeadlandOnOff.Image = Properties.Resources.HeadlandOff;


            stripSectionColor.BackColor = sectionColorDay;

            if (Properties.Settings.Default.setDisplay_isTermsOn)
            {
                using (var form = new Form_First())
                {
                    var result = form.ShowDialog();
                    if (result != DialogResult.OK)
                    {
                        Close();
                    }
                }
            }

            if (Properties.Settings.Default.setDisplay_isStartFullScreen)
            {
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                btnFullScreen.BackgroundImage = Properties.Resources.WindowNormal;
                isFullScreen = true;
            }
            else
            {
                isFullScreen = false;
            }

            //is rtk on?
            isRTK = Properties.Settings.Default.setGPS_isRTK;

            //update the field data areas
            fd.UpdateFieldBoundaryGUIAreas();

        }

        public void SwapDayNightMode()
        {
            isDay = !isDay;
            if (isDay)
            {
                btnDayNightMode.Image = Properties.Resources.WindowNightMode;

                this.BackColor = dayColor;
                foreach (Control c in this.Controls)
                {
                    //if (c is Label || c is Button)
                    {
                        c.ForeColor = Color.Black;
                    }
                }
                LineUpManualBtns();
            }
            else //nightmode
            {
                btnDayNightMode.Image = Properties.Resources.WindowDayMode;
                this.BackColor = nightColor;

                foreach (Control c in this.Controls)
                {
                    {
                        c.ForeColor = Color.White;
                    }
                }
                LineUpManualBtns();
            }

            Properties.Settings.Default.setDisplay_isDayMode = isDay;
            Properties.Settings.Default.Save();
        }

        private void FixPanelsAndMenus()
        {

            //if (panelSim.Left + 443 > Width - 200) panelSim.Left = Width - 200 - 443;
            //if (panelSim.Top < 80) panelSim.Top = 80;
            //if (panelSim.Top > Height - 150) panelSim.Top = Height - 150;


            //if (panelSim.Left < 75) panelSim.Left = 75;

            if (!isSimple)
            {
                toolToolbottomStripBtn.Visible = true;
                vehicleToolStripBtn.Visible = true;
                AutoSteerToolBtn.Visible = true;

                lblDateTime.Visible = false;
                snapLeftBigStrip.Visible = false;
                snapRightBigStrip.Visible = false;

            }

            if (Width > 1100)
            {
                snapLeftBigStrip.Visible = true;
                snapRightBigStrip.Visible = true;
            }
            else
            {
                snapLeftBigStrip.Visible = false;
                snapRightBigStrip.Visible = false;
            }


            if (Width > 1300)
            {
                lblDateTime.Visible = true;
            }
            else
            {
                lblDateTime.Visible = false;
            }
            if (Width > 1400)
            {
            }
            else
            {
            }

            if (isSimple)
            {
                toolToolbottomStripBtn.Visible = false;
                vehicleToolStripBtn.Visible = false;
                AutoSteerToolBtn.Visible = false;

                lblDateTime.Visible = true;
                snapLeftBigStrip.Visible = true;
                snapRightBigStrip.Visible = true;
            }
        }

        public string FindDirection(double heading)
        {
            if (heading < 0) heading += glm.twoPI;

            heading = glm.toDegrees(heading);

            if (heading > 337.5 || heading < 22.5)
            {
                return (" " +  gStr.gsNorth + " ");
            }
            if (heading > 22.5 && heading < 67.5)
            {
                return (" " +  gStr.gsN_East + " ");
            }
            if (heading > 67.5 && heading < 111.5)
            {
                return (" " +  gStr.gsEast + " ");
            }
            if (heading > 111.5 && heading < 157.5)
            {
                return (" " +  gStr.gsS_East + " ");
            }
            if (heading > 157.5 && heading < 202.5)
            {
                return (" " +  gStr.gsSouth + " ");
            }
            if (heading > 202.5 && heading < 247.5)
            {
                return (" " +  gStr.gsS_West + " ");
            }
            if (heading > 247.5 && heading < 292.5)
            {
                return (" " +  gStr.gsWest + " ");
            }
            if (heading > 292.5 && heading < 337.5)
            {
                return (" " +  gStr.gsN_West + " ");
            }
            return (" " +  gStr.gsLost + " ");
        }

        //hide the left panel
        public void SwapBatmanPanels()
        {
            //no side panel
            oglMain.Left = statusStripLeft.Width;
            if (isSimple)
            {
                oglMain.Width = Width - (statusStripLeft.Width) - 110;
                if (isFullScreen) oglMain.Width += 20;
            }
            else
            {
                oglMain.Width = Width - (statusStripLeft.Width) - 198;
                if (isFullScreen) oglMain.Width += 20;
            }

            panelSim.Left = 100;
            panelSim.Width = Width - statusStripLeft.Width - 350;

            //if (isFullScreen) panelFieldData.Width += 20;

            if (panelDrag.Visible) panelDrag.Left = statusStripLeft.Width + 10;
        }

        //line up section On Off Auto buttons based on how many there are
        public void LineUpManualBtns()
        {
            int oglCenter = 0;

            oglCenter = statusStripLeft.Width + oglMain.Width / 2; 

            int top = 180;
            if (panelSim.Visible == true) top = 230;

            btnSection1Man.Top  = Height - top;
            btnSection2Man.Top  = Height - top;
            btnSection3Man.Top  = Height - top;
            btnSection4Man.Top  = Height - top;
            btnSection5Man.Top  = Height - top;
            btnSection6Man.Top  = Height - top;
            btnSection7Man.Top  = Height - top;
            btnSection8Man.Top  = Height - top;
            btnSection9Man.Top  = Height - top;
            btnSection10Man.Top = Height - top;
            btnSection11Man.Top = Height - top;
            btnSection12Man.Top = Height - top;
            btnSection13Man.Top = Height - top;
            btnSection14Man.Top = Height - top;
            btnSection15Man.Top = Height - top;
            btnSection16Man.Top = Height - top;

            int oglButtonWidth = oglMain.Width * 3/4;
            //if (tool.numOfSections < 9 )  oglButtonWidth = oglMain.Width * 5/6;

            int buttonMaxWidth = 400, buttonHeight = 30;

            int buttonWidth = oglButtonWidth / tool.numOfSections;
            if (buttonWidth > buttonMaxWidth) buttonWidth = buttonMaxWidth;
            btnSection1Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection2Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection3Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection4Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection5Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection6Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection7Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection8Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection9Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection10Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection11Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection12Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection13Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection14Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection15Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnSection16Man.Size = new System.Drawing.Size(buttonWidth, buttonHeight);

            switch (tool.numOfSections)
            {
                case 1:
                    btnSection1Man.Left = (oglCenter) - btnSection1Man.Size.Width/2;
                    break;

                case 2:                    
                    btnSection1Man.Left = (oglCenter) - btnSection1Man.Size.Width;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    break;

                case 3:
                    btnSection1Man.Left = (oglCenter) - btnSection1Man.Size.Width/2 - btnSection1Man.Size.Width;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    break;

                case 4:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;

                    break;

                case 5:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    break;

                case 6:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width)/2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    break;

                case 7:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    break;

                case 8:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    break;

                case 9:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    break;

                case 10:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    btnSection10Man.Left = btnSection9Man.Left + btnSection1Man.Size.Width;
                    break;

                case 11:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    btnSection10Man.Left = btnSection9Man.Left + btnSection1Man.Size.Width;
                    btnSection11Man.Left = btnSection10Man.Left + btnSection1Man.Size.Width;
                    break;

                case 12:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    btnSection10Man.Left = btnSection9Man.Left + btnSection1Man.Size.Width;
                    btnSection11Man.Left = btnSection10Man.Left + btnSection1Man.Size.Width;
                    btnSection12Man.Left = btnSection11Man.Left + btnSection1Man.Size.Width;
                    break;
                case 13:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    btnSection10Man.Left = btnSection9Man.Left + btnSection1Man.Size.Width;
                    btnSection11Man.Left = btnSection10Man.Left + btnSection1Man.Size.Width;
                    btnSection12Man.Left = btnSection11Man.Left + btnSection1Man.Size.Width;
                    btnSection13Man.Left = btnSection12Man.Left + btnSection1Man.Size.Width;
                    break;
                case 14:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    btnSection10Man.Left = btnSection9Man.Left + btnSection1Man.Size.Width;
                    btnSection11Man.Left = btnSection10Man.Left + btnSection1Man.Size.Width;
                    btnSection12Man.Left = btnSection11Man.Left + btnSection1Man.Size.Width;
                    btnSection13Man.Left = btnSection12Man.Left + btnSection1Man.Size.Width;
                    btnSection14Man.Left = btnSection13Man.Left + btnSection1Man.Size.Width;
                    break;
                case 15:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    btnSection10Man.Left = btnSection9Man.Left + btnSection1Man.Size.Width;
                    btnSection11Man.Left = btnSection10Man.Left + btnSection1Man.Size.Width;
                    btnSection12Man.Left = btnSection11Man.Left + btnSection1Man.Size.Width;
                    btnSection13Man.Left = btnSection12Man.Left + btnSection1Man.Size.Width;
                    btnSection14Man.Left = btnSection13Man.Left + btnSection1Man.Size.Width;
                    btnSection15Man.Left = btnSection14Man.Left + btnSection1Man.Size.Width;
                    break;
                case 16:
                    btnSection1Man.Left = (oglCenter) - (tool.numOfSections * btnSection1Man.Size.Width) / 2;
                    btnSection2Man.Left = btnSection1Man.Left + btnSection1Man.Size.Width;
                    btnSection3Man.Left = btnSection2Man.Left + btnSection1Man.Size.Width;
                    btnSection4Man.Left = btnSection3Man.Left + btnSection1Man.Size.Width;
                    btnSection5Man.Left = btnSection4Man.Left + btnSection1Man.Size.Width;
                    btnSection6Man.Left = btnSection5Man.Left + btnSection1Man.Size.Width;
                    btnSection7Man.Left = btnSection6Man.Left + btnSection1Man.Size.Width;
                    btnSection8Man.Left = btnSection7Man.Left + btnSection1Man.Size.Width;
                    btnSection9Man.Left = btnSection8Man.Left + btnSection1Man.Size.Width;
                    btnSection10Man.Left = btnSection9Man.Left + btnSection1Man.Size.Width;
                    btnSection11Man.Left = btnSection10Man.Left + btnSection1Man.Size.Width;
                    btnSection12Man.Left = btnSection11Man.Left + btnSection1Man.Size.Width;
                    btnSection13Man.Left = btnSection12Man.Left + btnSection1Man.Size.Width;
                    btnSection14Man.Left = btnSection13Man.Left + btnSection1Man.Size.Width;
                    btnSection15Man.Left = btnSection14Man.Left + btnSection1Man.Size.Width;
                    btnSection16Man.Left = btnSection15Man.Left + btnSection1Man.Size.Width;
                    break;
            }

            //if (isJobStarted)
            {
                switch (tool.numOfSections)
                {
                    case 1:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = false;
                        btnSection3Man.Visible = false;
                        btnSection4Man.Visible = false;
                        btnSection5Man.Visible = false;
                        btnSection6Man.Visible = false;
                        btnSection7Man.Visible = false;
                        btnSection8Man.Visible = false;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 2:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = false;
                        btnSection4Man.Visible = false;
                        btnSection5Man.Visible = false;
                        btnSection6Man.Visible = false;
                        btnSection7Man.Visible = false;
                        btnSection8Man.Visible = false;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 3:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = false;
                        btnSection5Man.Visible = false;
                        btnSection6Man.Visible = false;
                        btnSection7Man.Visible = false;
                        btnSection8Man.Visible = false;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 4:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = false;
                        btnSection6Man.Visible = false;
                        btnSection7Man.Visible = false;
                        btnSection8Man.Visible = false;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 5:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = false;
                        btnSection7Man.Visible = false;
                        btnSection8Man.Visible = false;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 6:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = false;
                        btnSection8Man.Visible = false;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 7:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = false;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 8:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = false;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 9:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = false;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 10:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = true;
                         btnSection11Man.Visible = false;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 11:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = true;
                         btnSection11Man.Visible = true;
                         btnSection12Man.Visible = false;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 12:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = true;
                         btnSection11Man.Visible = true;
                         btnSection12Man.Visible = true;
                         btnSection13Man.Visible = false;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 13:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = true;
                         btnSection11Man.Visible = true;
                         btnSection12Man.Visible = true;
                         btnSection13Man.Visible = true;
                         btnSection14Man.Visible = false;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 14:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = true;
                         btnSection11Man.Visible = true;
                         btnSection12Man.Visible = true;
                         btnSection13Man.Visible = true;
                         btnSection14Man.Visible = true;
                         btnSection15Man.Visible = false;
                         btnSection16Man.Visible = false;
                        break;

                    case 15:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = true;
                         btnSection11Man.Visible = true;
                         btnSection12Man.Visible = true;
                         btnSection13Man.Visible = true;
                         btnSection14Man.Visible = true;
                         btnSection15Man.Visible = true;
                         btnSection16Man.Visible = false;
                        break;

                    case 16:
                        btnSection1Man.Visible = true;
                        btnSection2Man.Visible = true;
                        btnSection3Man.Visible = true;
                        btnSection4Man.Visible = true;
                        btnSection5Man.Visible = true;
                        btnSection6Man.Visible = true;
                        btnSection7Man.Visible = true;
                        btnSection8Man.Visible = true;
                        btnSection9Man.Visible = true;
                         btnSection10Man.Visible = true;
                         btnSection11Man.Visible = true;
                         btnSection12Man.Visible = true;
                         btnSection13Man.Visible = true;
                         btnSection14Man.Visible = true;
                         btnSection15Man.Visible = true;
                         btnSection16Man.Visible = true;
                        break;
                }
            }
        }

        //force all the buttons same according to two main buttons
        private void ManualAllBtnsUpdate()
        {
            ManualBtnUpdate(0, btnSection1Man);
            ManualBtnUpdate(1, btnSection2Man);
            ManualBtnUpdate(2, btnSection3Man);
            ManualBtnUpdate(3, btnSection4Man);
            ManualBtnUpdate(4, btnSection5Man);
            ManualBtnUpdate(5, btnSection6Man);
            ManualBtnUpdate(6, btnSection7Man);
            ManualBtnUpdate(7, btnSection8Man);
            ManualBtnUpdate(8, btnSection9Man);
            ManualBtnUpdate(9, btnSection10Man);
            ManualBtnUpdate(10, btnSection11Man);
            ManualBtnUpdate(11, btnSection12Man);
            ManualBtnUpdate(12, btnSection13Man);
            ManualBtnUpdate(13, btnSection14Man);
            ManualBtnUpdate(14, btnSection15Man);
            ManualBtnUpdate(15, btnSection16Man);

        }
        //update individual btn based on state after push

        private void ManualBtnUpdate(int sectNumber, Button btn)
        {
            switch (section[sectNumber].manBtnState)
            {
                case manBtn.Off:
                    section[sectNumber].manBtnState = manBtn.Auto;
                    if (isDay)
                    {
                        btn.BackColor = Color.Lime;
                        btn.ForeColor = Color.Black;
                    }
                    else
                    {
                        btn.BackColor = Color.ForestGreen;
                        btn.ForeColor = Color.White;
                    }
                    break;
            

                case manBtn.Auto:
                    section[sectNumber].manBtnState = manBtn.On;
                    if (isDay)
                    {
                        btn.BackColor = Color.Yellow;
                        btn.ForeColor = Color.Black;
                    }
                    else
                    {
                        btn.BackColor = Color.DarkGoldenrod;
                        btn.ForeColor = Color.White;
                    }
                    break;

                case manBtn.On:
                    section[sectNumber].manBtnState = manBtn.Off;
                    if (isDay)
                    {
                        btn.ForeColor = Color.Black;
                        btn.BackColor = Color.Red;
                    }
                    else
                    {
                        btn.BackColor = Color.Crimson;
                        btn.ForeColor = Color.White;
                    }
                    break;
            }
        }

        //Mouse Clicks 
        private void oglMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //0 at bottom for opengl, 0 at top for windows, so invert Y value
                Point point = oglMain.PointToClient(Cursor.Position);

                //label3.Text = point.X.ToString();

                if (point.Y < 140 && point.Y > 40)
                {
                    int middle = oglMain.Width / 2 + oglMain.Width / 5;
                    if (point.X > middle - 80 && point.X < middle + 80)
                    {
                        SwapDirection();
                        return;
                    }

                    middle = oglMain.Width / 2 - oglMain.Width / 4;
                    if (point.X > middle - 140 && point.X < middle)
                    {
                        if (yt.isYouTurnTriggered)
                        {
                            yt.ResetYouTurn();
                        }
                        else
                        {
                            if (yt.isYouTurnBtnOn) btnAutoYouTurn.PerformClick();
                            yt.isYouTurnTriggered = true;
                            yt.BuildManualYouTurn(false, true);
                            return;
                        }
                    }

                    if (point.X > middle && point.X < middle + 140)
                    {
                        if (yt.isYouTurnTriggered)
                        {
                            yt.ResetYouTurn();
                        }
                        else
                        {
                            if (yt.isYouTurnBtnOn) btnAutoYouTurn.PerformClick();
                            yt.isYouTurnTriggered = true;
                            yt.BuildManualYouTurn(true, true);
                            return;
                        }
                    }
                }

                //prevent flag selection if flag form is up
                Form fc = Application.OpenForms["FormFlags"];
                if (fc != null)
                {
                    fc.Focus();
                    return;
                }

                mouseX = point.X;
                mouseY = oglMain.Height - point.Y;
                leftMouseDownOnOpenGL = true;
            }
        }
        private void oglZoom_MouseClick(object sender, MouseEventArgs e)
        {
            if ((sender as Control).IsDragging()) return;

            if (oglZoom.Width == 180)
            {
                oglZoom.Width = 300;
                oglZoom.Height = 300;
            }

            else if (oglZoom.Width == 300)
            {
                oglZoom.Width = 180;
                oglZoom.Height = 180;
            }
        } 
        
        //Function to delete flag
        public void DeleteSelectedFlag()
        {
            //delete selected flag and set selected to none
            flagPts.RemoveAt(flagNumberPicked - 1);
            flagNumberPicked = 0;

            // re-sort the id's based on how many flags left
            int flagCnt = flagPts.Count;
            if (flagCnt > 0)
            {
                for (int i = 0; i < flagCnt; i++) flagPts[i].ID = i + 1;
            }
        }
        public void EnableYouTurnButtons()
        {
            yt.ResetYouTurn();

            yt.isYouTurnBtnOn = false;
            btnAutoYouTurn.Enabled = true;
            btnAutoYouTurn.Image = Properties.Resources.YouTurnNo;
        }
        public void DisableYouTurnButtons()
        {

            btnAutoYouTurn.Enabled = false;
            yt.isYouTurnBtnOn = false;
            btnAutoYouTurn.Image = Properties.Resources.YouTurnNo;
            yt.ResetYouTurn();
        }

        private void DoNTRIPSecondRoutine()
        {
            //count up the ntrip clock only if everything is alive
            if (startCounter > 50 && recvCounter < 20 && isNTRIP_RequiredOn)
            {
                IncrementNTRIPWatchDog();
            }

            //Have we connection
            if (isNTRIP_RequiredOn && !isNTRIP_Connected && !isNTRIP_Connecting)
            {
                if (!isNTRIP_Starting && ntripCounter > 20)
                {
                    StartNTRIP();
                }
            }

            if (isNTRIP_Connecting)
            {
                if (ntripCounter > 28)
                {
                    TimedMessageBox(2000, gStr.gsSocketConnectionProblem, gStr.gsNotConnectingToCaster);
                    ReconnectRequest();
                }
                if (clientSocket != null && clientSocket.Connected)
                {
                    //TimedMessageBox(2000, "NTRIP Not Connected", " At the StartNTRIP() ");
                    //ReconnectRequest();
                    //return;
                    SendAuthorization();
                }

            }

            if (isNTRIP_RequiredOn)
            {
                //update byte counter and up counter
                if (ntripCounter > 59) NTRIPStartStopStrip.Text = (ntripCounter / 60) + " Mins";
                else if (ntripCounter < 60 && ntripCounter > 22) NTRIPStartStopStrip.Text = ntripCounter + " Secs";
                else NTRIPStartStopStrip.Text = "In " + (Math.Abs(ntripCounter - 22)) + " secs";

                pbarNtripMenu.Value = unchecked((byte)(tripBytes * 0.02));
                NTRIPBytesMenu.Text = ((tripBytes) * 0.001).ToString("###,###,###") + " kb";

                //watchdog for Ntrip
                if (isNTRIP_Connecting) lblWatch.Text = gStr.gsAuthourizing;
                else
                {
                    if (NTRIP_Watchdog > 10) lblWatch.Text = gStr.gsWaiting;
                    else lblWatch.Text = gStr.gsListening;
                }

                if (sendGGAInterval > 0 && isNTRIP_Sending)
                {
                    lblWatch.Text = "Send GGA";
                    isNTRIP_Sending = false;
                }
            }
        }

        private void ShowNoGPSWarning()
        {
            //update main window
            sentenceCounter = 100;
            oglMain.MakeCurrent();
            oglMain.Refresh();
        }




        #region Properties // ---------------------------------------------------------------------

        public string FixNorthing { get { return Convert.ToString(Math.Round(pn.fix.northing, 2)); } }
        public string FixEasting { get { return Convert.ToString(Math.Round(pn.fix.easting, 2)); } }
        public string Latitude { get { return Convert.ToString(Math.Round(pn.latitude, 7)); } }
        public string Longitude { get { return Convert.ToString(Math.Round(pn.longitude, 7)); } }

        public string SatsTracked { get { return Convert.ToString(pn.satellitesTracked); } }
        public string HDOP { get { return Convert.ToString(pn.hdop); } }
        public string PassNumber { get { return Convert.ToString(ABLine.passNumber); } }
        public string CurveNumber { get { return Convert.ToString(curve.curveNumber); } }
        public string Heading { get { return Convert.ToString(Math.Round(glm.toDegrees(fixHeading), 1)) + "\u00B0"; } }
        public string GPSHeading { get { return (Math.Round(glm.toDegrees(gpsHeading), 1)) + "\u00B0"; } }
        public string Status { get { if (pn.status == "A") return "Active"; else return "Void"; } }
        public string FixQuality
        {
            get
            {
                if (timerSim.Enabled)
                    return "Sim: ";
                else if (pn.fixQuality == 0) return "Invalid: ";
                else if (pn.fixQuality == 1) return "GPS single: ";
                else if (pn.fixQuality == 2) return "DGPS : ";
                else if (pn.fixQuality == 3) return "PPS : ";
                else if (pn.fixQuality == 4) return "RTK fix: ";
                else if (pn.fixQuality == 5) return "Float: ";
                else if (pn.fixQuality == 6) return "Estimate: ";
                else if (pn.fixQuality == 7) return "Man IP: ";
                else if (pn.fixQuality == 8) return "Sim: ";
                else return "Unknown: ";
            }
        }

        public string GyroInDegrees
        {
            get
            {
                if (ahrs.correctionHeadingX16 != 9999)
                    return Math.Round(ahrs.correctionHeadingX16 * 0.0625, 1) + "\u00B0";
                else return "-";
            }
        }
        public string RollInDegrees
        {
            get
            {
                if (ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR)
                    return Math.Round((ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625, 1) + "\u00B0";
                else return "-";
            }
        }
        public string SetSteerAngle { get { return ((double)(guidanceLineSteerAngle) * 0.01).ToString("N1") + "\u00B0"; } }
        public string ActualSteerAngle { get { return ((double)(actualSteerAngleDisp) * 0.01).ToString("N1") + "\u00B0"; } }

        public string FixHeading { get { return Math.Round(fixHeading, 4).ToString(); } }

        //public string LookAhead { get { return ((int)(section[0].lookAheadOn)).ToString(); } }
        public string StepFixNum { get { return (currentStepFix).ToString(); } }
        public string CurrentStepDistance { get { return Math.Round(distanceCurrentStepFix, 3).ToString(); } }
        public string TotalStepDistance { get { return Math.Round(fixStepDist, 3).ToString(); } }

        public string WorkSwitchValue { get { return mc.workSwitchValue.ToString(); } }
        public string AgeDiff { get { return pn.ageDiff.ToString(); } }

        //Metric and Imperial Properties
        public string SpeedMPH
        {
            get
            {
                return Convert.ToString(Math.Round(avgSpeed*0.62137, 1));
            }
        }
        public string SpeedKPH
        {
            get
            {
                return Convert.ToString(Math.Round(avgSpeed, 1));
            }
        }

        public string XTE
        {
            get
            {
                //double spd = 0;
                //for (int c = 0; c < 20; c++) spd += avgXTE[c];
                //spd *= 0.1;
                //return ((int)(spd * 0.05) + " cm");
                return (crossTrackError/10 + gStr.gsCM);
            }
        }
        public string InchXTE
        {
            get
            {
                //double spd = 0;
                //for (int c = 0; c < 20; c++) spd += avgXTE[c];
                //spd *= 0.1;
                //return ((int)(spd * 0.019685) + " in");
                return ((int)(crossTrackError/25.54) + " in");
            }
        }

        public string FixOffset { get { return (pn.fixOffset.easting.ToString("N2") + ", " + pn.fixOffset.northing.ToString("N2")); } }
        public string FixOffsetInch { get { return ((pn.fixOffset.easting*glm.m2in).ToString("N0")+ ", " + (pn.fixOffset.northing*glm.m2in).ToString("N0")); } }

        public string Altitude { get { return Convert.ToString(Math.Round(pn.altitude,1)); } }
        public string AltitudeFeet { get { return Convert.ToString((Math.Round((pn.altitude * 3.28084),1))); } }
        public string DistPivotM
        {
            get
            {
                if (distancePivotToTurnLine > 0 )
                    return ((int)(distancePivotToTurnLine)) + " m";
                else return "--";
            }
        }
        public string DistPivotFt
        {
            get
            {
                if (distancePivotToTurnLine > 0 ) return (((int)(glm.m2ft * (distancePivotToTurnLine))) + " ft");
                else return "--";
            }
        }

        #endregion properties 
    }//end class
}//end namespace