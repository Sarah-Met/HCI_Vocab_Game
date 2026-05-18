/*
	TUIO C# Demo - part of the reacTIVision project
	Copyright (c) 2005-2014 Martin Kaltenbrunner <martin@tuio.org>

	This program is free software; you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation; either version 2 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program; if not, write to the Free Software
	Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TUIO;
using System.Net.Sockets;
using System.Text;
using System.Drawing.Text;


public class TuioDemo : Form, TuioListener
{
    private TuioClient client;
    private Dictionary<long, TuioDemoObject> objectList;
    private Dictionary<long, TuioCursor> cursorList;
    private Dictionary<long, TuioBlob> blobList;
    private object cursorSync = new object();
    private object objectSync = new object();
    private object blobSync = new object();

    public Bitmap userslist = new Bitmap("Userlist.png");
    public Bitmap searchingfordevice = new Bitmap("searchingfordevices.png");
    public Bitmap loginBtn = new Bitmap("login.png");
    public Bitmap welcomeback = new Bitmap("welcomeback page.png");
    public Bitmap boyBg = new Bitmap("Boy BG.png");
    public Bitmap girlBg = new Bitmap("Girl BG.png");
    public Bitmap winBg = null; // loaded from win.png if present
    public Bitmap selectBg = null; // loaded from select.png for mode selection screen
    public Bitmap whiteFrame = new Bitmap("white frame.png");
    public Bitmap yellowFrame = null; // loaded from "yellow.png" if present
    public Bitmap startBtn = new Bitmap("Start Button.png");
    public Bitmap checkBtn = new Bitmap("Start Button.png");
    public static int width, height;

    private PrivateFontCollection pfcPetit = new PrivateFontCollection();
    private PrivateFontCollection pfcMont = new PrivateFontCollection();

    public Font petitFont;
    public Font montserratFont;
    public Font feedbackFont;
    private int window_width = 1920;
    private int window_height = 1080;
    private int menuRadius = 220;
    private int menuItemSize = 90;
    private int window_left = 0;
    private int window_top = 0;
    private int screen_width = Screen.PrimaryScreen.Bounds.Width;
    private int screen_height = Screen.PrimaryScreen.Bounds.Height;
    private int currentPage = 0; // 0 = Login, 1 = Sign Up, 2 = Vocab
    private bool fullscreen;
    private bool verbose;

    private string[] subjectOptions = { "I", "You", "He", "She" };
    private string[] verbOptions = { "eat", "read", "kick", "drink" };
    private string[] objectOptions = { "apple", "book", "ball", "milk" };

    // Loads word pools from words.json written by the teacher panel.
    // Falls back to the hardcoded defaults above if the file is missing or broken.
    private string[] circleOptions = { "NEXT", "PREV", "CLOSE", "RESET" };

    private int selectedSubject = 0;
    private int selectedVerb = 0;
    private int selectedObject = 0;
    private int circleindex = 0;

    private string feedbackMessage = "Show marker 0 on login to start the game.";
    private string emotionMessage = "";
    private string gazeHintMessage = "";
    private string gazeZone = "";
    private int gazeSelectedSlot = -1;
    private bool answerChecked = false;
    private bool circle = false;
    private bool menuVisible = false;
    private bool isCorrect = false;
    public NetworkStream stream;
    public TcpClient socketClient;
    public NetworkStream gestureStream;
    public TcpClient gestureSocketClient;
    Thread gestureSocketThread;
    SolidBrush blackBrush = new SolidBrush(Color.Black);
    SolidBrush whiteBrush = new SolidBrush(Color.White);

    public Brush subjectBrush = new SolidBrush(ColorTranslator.FromHtml("#ff3131")); // Red
    public Brush verbBrush = new SolidBrush(ColorTranslator.FromHtml("#00bf63"));    // Green
    public Brush objectBrush = new SolidBrush(ColorTranslator.FromHtml("#4849e8"));  // Blue

    Pen fingerPen = new Pen(new SolidBrush(Color.Blue), 1);
    SolidBrush grayBrush = new SolidBrush(Color.Gray);


    Thread socketThread;
    int gestureSelectedSlot = 0;
    bool gestureMode = false;

    string[] gestureSubjectOptions = { "I", "YOU", "HE", "SHE" };
    string[] gestureVerbOptions = { "EAT", "PLAY", "READ", "DRINK" };
    string[] gestureObjectOptions = { "APPLE", "BALL", "BOOK", "WATER" };

    int gestureSubject = 0;
    int gestureVerb = 0;
    int gestureObject = 0;

    // Used by camera gestures on the normal sentence pages (3, 4, 5, 6).
    // 0 = subject box, 1 = verb box, 2 = object box.
    int cameraGestureSlot = 0;

    bool gestureAnswerChecked = false;
    bool gestureIsCorrect = false;
    private string currentUser = "";
    private string currentUserG = "";
    private int currentLevel = 1;
    private string currentLevelAnswer = ""; // correct answer for current level, sent by Python
    private int totalLevels = 0; // set by TOTALLEVELS message from Python after login
    private int fingerDotX = -1; // fingertip position for dot overlay
    private int fingerDotY = -1;
    private bool showWinScreen = false;
    private bool laserMode = false;      // true = laser mode, false = gesture mode
    private bool laserLocked = false;    // true = slot locked, pointing won't change it
    private int laserSlot = 0;          // 0=subject, 1=verb, 2=object
    public TuioDemo(int port)
    {
        try
        {
            string font1Path = System.IO.Path.Combine(Application.StartupPath, "petit cochon.ttf");
            string font2Path = System.IO.Path.Combine(Application.StartupPath, "Montserrat-Bold.ttf");

            // Load Petit Cochon
            if (System.IO.File.Exists(font1Path))
            {
                pfcPetit.AddFontFile(font1Path);
                petitFont = new Font(pfcPetit.Families[0], 50, FontStyle.Regular);
                feedbackFont = new Font(pfcPetit.Families[0], 38, FontStyle.Regular);
            }
            else
            {
                MessageBox.Show("Could not find: petit cochon.ttf");
            }

            if (System.IO.File.Exists(font2Path))
            {
                pfcMont.AddFontFile(font2Path);

                montserratFont = new Font(pfcMont.Families[0], 30, FontStyle.Regular);
            }
            else
            {
                MessageBox.Show("Could not find: Montserrat-Bold.ttf");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Font Error: " + ex.Message);
        }

        verbose = true;
        fullscreen = false;
        width = window_width;
        height = window_height;

        this.ClientSize = new System.Drawing.Size(width, height);
        this.Name = "TuioDemo";
        this.Text = "TuioDemo";

        this.Closing += new CancelEventHandler(Form_Closing);
        this.KeyDown += new KeyEventHandler(Form_KeyDown);
        this.MouseClick += new MouseEventHandler(Form_MouseClick);

        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                        ControlStyles.UserPaint |
                        ControlStyles.DoubleBuffer, true);

        objectList = new Dictionary<long, TuioDemoObject>(128);
        cursorList = new Dictionary<long, TuioCursor>(128);
        blobList = new Dictionary<long, TuioBlob>(128);

        client = new TuioClient(port);
        client.addTuioListener(this);
        socketThread = new Thread(streamSocket);
        socketThread.IsBackground = true;
        socketThread.Start();

        // IMPORTANT:
        // Python currently accepts only ONE socket connection on port 5000.
        // The main socketThread is enough because it receives LOGIN, GESTURE, and OBJECT messages.
        // So the second gesture socket thread is disabled to avoid duplicate connection problems.
        // gestureSocketThread = new Thread(streamGestureSocket);
        // gestureSocketThread.IsBackground = true;
        // gestureSocketThread.Start();

        client.connect();

        try
        {
            string winPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "win.png");
            if (System.IO.File.Exists(winPath))
                winBg = new Bitmap(winPath);
            else
                Console.WriteLine("win.png not found at: " + winPath);
        }
        catch (Exception ex) { Console.WriteLine("win.png load error: " + ex.Message); }

        try
        {
            string selectPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "select.png");
            if (System.IO.File.Exists(selectPath))
                selectBg = new Bitmap(selectPath);
            else
                Console.WriteLine("select.png not found at: " + selectPath);
        }
        catch (Exception ex) { Console.WriteLine("select.png load error: " + ex.Message); }
        try
        {
            string yellowPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "yellow.png");
            if (System.IO.File.Exists(yellowPath))
                yellowFrame = new Bitmap(yellowPath);
        }
        catch { }

    }

    public bool connectToSocket(string host, int portNumber)
    {
        try
        {
            socketClient = new TcpClient(host, portNumber);
            stream = socketClient.GetStream();
            Console.WriteLine("connection made ! with " + host);
            SendToServer("WINDOWSIZE:" + this.ClientSize.Width);
            return true;
        }
        catch (System.Net.Sockets.SocketException e)
        {
            Console.WriteLine("Connection Failed: " + e.Message);
            return false;
        }
    }

    public string recieveMessage()
    {
        try
        {
            byte[] receiveBuffer = new byte[1024];
            int bytesReceived = stream.Read(receiveBuffer, 0, 1024);

            if (bytesReceived <= 0)
            {
                return null;
            }

            Console.WriteLine(bytesReceived);

            string data = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);
            Console.WriteLine(data);

            return data;
        }
        catch (System.Exception e)
        {
            Console.WriteLine("Receive Error: " + e.Message);
        }

        return null;
    }

    // sends a message back to Python through the socket
    public void SendToServer(string msg)
    {
        try
        {
            if (stream != null && socketClient != null && socketClient.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                stream.Write(data, 0, data.Length);
                Console.WriteLine("Sent to Python: " + msg);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("SendToServer error: " + e.Message);
        }
    }

    public void streamSocket()
    {
        connectToSocket("localhost", 5000);
        string buffer = "";

        while (true)
        {
            string raw = recieveMessage();

            if (raw == null)
                break;

            buffer += raw;

            // Split on newline — each message from Python ends with \n
            string[] parts = buffer.Split('\n');

            // Last element may be incomplete — keep it in buffer
            buffer = parts[parts.Length - 1];

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string msg = parts[i].Trim();
                if (msg.Length == 0) continue;

                string msgCopy = msg;
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        ProcessSocketMessage(msgCopy);
                    }));
                }
                else
                {
                    ProcessSocketMessage(msgCopy);
                }

                if (msgCopy == "q")
                {
                    stream.Close();
                    socketClient.Close();
                    Console.WriteLine("Connection Terminated !");
                    break;
                }
            } // end for loop
        }
    }

    private void ProcessSocketMessage(string msg)
    {
        Console.WriteLine("MSG = [" + msg + "]");

        if (msg.StartsWith("LOGIN:"))
        {
            string data = msg.Substring(6).Trim();
            string[] parts = data.Split(':');

            if (parts.Length >= 1)
            {
                currentUser = parts[0];

                if (parts.Length >= 2)
                    currentUserG = parts[1];
                else
                    currentUserG = "m";

                // parse level from login message
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[2], out currentLevel);
                    if (currentLevel < 1) currentLevel = 1;
                }
                else
                {
                    currentLevel = 1;
                }

                currentPage = 2;
                feedbackMessage = "Welcome " + currentUser + " (Level " + currentLevel + ")";
            }

            answerChecked = false;
            Invalidate();
        }
        else if (msg.StartsWith("GESTURE:"))
        {
            string gestureName = msg.Substring(8).Trim().ToUpper();

            if (currentPage == 10)
            {
                if (gestureName == "CIRCLE")
                {
                    laserMode = false;
                    laserLocked = false;
                    SendToServer("MODE:GESTURE");
                    currentPage = (currentUserG == "m") ? 3 : 5;
                    Invalidate();
                }
                else if (gestureName == "FIST")
                {
                    laserMode = true;
                    laserLocked = false;
                    SendToServer("MODE:LASER");
                    currentPage = (currentUserG == "m") ? 3 : 5;
                    Invalidate();
                }
                return;
            }
            else if (currentPage == 7 || currentPage == 8)
            {
                HandleGesture(gestureName);
            }
            else if (currentPage == 3 || currentPage == 5)
            {
                if (laserMode)
                    HandleLaserGesture(gestureName);
                else
                    HandleSentenceGesture(gestureName);
            }
        }
        else if (msg.StartsWith("OBJECT:"))
        {
            string objectName = msg.Substring(7).Trim().ToUpper();

            if (objectName == "BALL")
            {
                currentPage = (currentUserG == "m") ? 3 : 5;
                selectedSubject = 2; // HE
                selectedVerb = 2;    // KICK
                selectedObject = 2;  // BALL
                feedbackMessage = "Ball detected!";
                cameraGestureSlot = 0;
            }
            else if (objectName == "BOOK")
            {
                currentPage = (currentUserG == "m") ? 3 : 5;
                selectedSubject = 1; // YOU
                selectedVerb = 1;    // READ
                selectedObject = 1;  // BOOK
                feedbackMessage = "Book detected!";
                cameraGestureSlot = 0;
            }
            else if (objectName == "APPLE")
            {
                currentPage = (currentUserG == "m") ? 3 : 5;
                selectedSubject = 0; // I
                selectedVerb = 0;    // EAT
                selectedObject = 0;  // APPLE
                feedbackMessage = "Apple detected!";
                cameraGestureSlot = 0;
            }

            answerChecked = false;
            isCorrect = false;
            Invalidate();
        }
        else if (msg.StartsWith("EMOTION:"))
        {
            string emotionName = msg.Substring(8).Trim().ToUpper();

            if (emotionName == "HAPPY")
            {
                emotionMessage = "You look happy! Keep going!";
            }
            else if (emotionName == "NEUTRAL")
            {
                emotionMessage = "Focus on the sentence and choose carefully.";
            }
            else if (emotionName == "SAD")
            {
                emotionMessage = "Take your time. You can do it!";
            }
            else if (emotionName == "ANGRY" || emotionName == "FEAR")
            {
                emotionMessage = "Do not worry. Try slowly step by step.";
            }
            else if (emotionName == "SURPRISE")
            {
                emotionMessage = "Good reaction! Look at the correct word.";
            }

            Invalidate();
        }
        else if (msg.StartsWith("GAZE:"))
        {
            string zoneName = msg.Substring(5).Trim().ToUpper();
            gazeZone = zoneName;

            if (zoneName == "SUBJECT")
            {
                cameraGestureSlot = 0;
                gazeSelectedSlot = 0;
                gazeHintMessage = "Subject area: choose who is doing the action.";
            }
            else if (zoneName == "VERB")
            {
                cameraGestureSlot = 1;
                gazeSelectedSlot = 1;
                gazeHintMessage = "Verb area: choose the action word.";
            }
            else if (zoneName == "OBJECT")
            {
                cameraGestureSlot = 2;
                gazeSelectedSlot = 2;
                gazeHintMessage = "Object area: choose the thing or object.";
            }
            else if (zoneName == "SENTENCE")
            {
                gazeSelectedSlot = -1;
                gazeHintMessage = "Read the full sentence before checking.";
            }
            else if (zoneName == "FEEDBACK")
            {
                gazeSelectedSlot = -1;
                gazeHintMessage = "Use the fist gesture to check your answer.";
            }

            Invalidate();
        }
        else if (msg.StartsWith("WORDS:"))
        {
            // word pool update from Python
            string[] parts = msg.Substring(6).Split(':');
            if (parts.Length == 2)
            {
                string category = parts[0].Trim().ToLower();
                string[] words = parts[1].Split(',');

                for (int i = 0; i < words.Length; i++)
                    words[i] = words[i].Trim();

                if (category == "subjects")
                {
                    subjectOptions = words;
                    gestureSubjectOptions = words.Select(w => w.ToUpper()).ToArray();
                    selectedSubject = 0;
                    gestureSubject = 0;
                }
                else if (category == "verbs")
                {
                    verbOptions = words;
                    gestureVerbOptions = words.Select(w => w.ToUpper()).ToArray();
                    selectedVerb = 0;
                    gestureVerb = 0;
                }
                else if (category == "objects")
                {
                    objectOptions = words;
                    gestureObjectOptions = words.Select(w => w.ToUpper()).ToArray();
                    selectedObject = 0;
                    gestureObject = 0;
                }

                Console.WriteLine("Words updated - " + category + ": " + string.Join(", ", words));
                Invalidate();
            }
        }
        else if (msg.StartsWith("TOTALLEVELS:"))
        {
            int.TryParse(msg.Substring(12).Trim(), out totalLevels);
            Console.WriteLine("Total levels: " + totalLevels);
            Invalidate();
        }
        else if (msg.StartsWith("LEVELANSWER:"))
        {
            // correct answer for this level, sent by Python after login or level up
            currentLevelAnswer = msg.Substring(12).Trim();
            answerChecked = false;
            isCorrect = false;
            selectedSubject = 0;
            selectedVerb = 0;
            selectedObject = 0;
            Console.WriteLine("Level answer: " + currentLevelAnswer);
            Invalidate();
        }
        else if (msg.StartsWith("FINGER:"))
        {
            // fingertip pixel position from Python
            string coords = msg.Substring(7).Trim();
            string[] xy = coords.Split(',');
            if (xy.Length == 2)
            {
                int.TryParse(xy[0], out fingerDotX);
                int.TryParse(xy[1], out fingerDotY);
                Invalidate();
            }
        }
        else if (msg.StartsWith("ZONE:"))
        {
            string zoneName = msg.Substring(5).Trim().ToUpper();

            // laser mode: zone changes the active slot unless locked
            if (laserMode && !laserLocked)
            {
                if (zoneName == "SUBJECT") { laserSlot = 0; cameraGestureSlot = 0; gazeHintMessage = "Pointing at: Subject"; }
                else if (zoneName == "VERB") { laserSlot = 1; cameraGestureSlot = 1; gazeHintMessage = "Pointing at: Verb"; }
                else if (zoneName == "OBJECT") { laserSlot = 2; cameraGestureSlot = 2; gazeHintMessage = "Pointing at: Object"; }
            }
            else if (!laserMode)
            {
                // gesture mode: just update the hint text
                if (zoneName == "SUBJECT") { gazeHintMessage = "Hand at subject area."; }
                else if (zoneName == "VERB") { gazeHintMessage = "Hand at verb area."; }
                else if (zoneName == "OBJECT") { gazeHintMessage = "Hand at object area."; }
            }

            Invalidate();
        }
    }




    private int GetRotationIndex(float angleRadians, int count)
    {
        double angleDegrees = angleRadians * 180.0 / Math.PI;

        while (angleDegrees < 0)
            angleDegrees += 360;

        while (angleDegrees >= 360)
            angleDegrees -= 360;

        double sectionSize = 360.0 / count;
        int index = (int)(angleDegrees / sectionSize);

        if (index < 0) index = 0;
        if (index >= count) index = count - 1;

        return index;
    }

    private void CheckSentence()
    {
        string sentence =
            subjectOptions[selectedSubject].ToUpper() + " " +
            verbOptions[selectedVerb].ToUpper() + " " +
            objectOptions[selectedObject].ToUpper();

        // Get the correct answer for the current level from Python's level list
        // Python sends LEVELANSWER:SUBJECT VERB OBJECT after login
        string expectedAnswer = currentLevelAnswer.ToUpper().Trim();
        isCorrect = (sentence == expectedAnswer);

        answerChecked = true;

        if (isCorrect)
        {
            int nextLevel = currentLevel + 1;

            if (nextLevel > totalLevels)
            {
                // All levels completed — show win screen
                showWinScreen = true;
                SendToServer("WIN");
            }
            else
            {
                currentLevel = nextLevel;
                SendToServer("LEVELUP:" + nextLevel);
            }
        }
    }
    private void Circlemenu()
    {
        if (circleindex == 0) // NEXT
        {
            if (currentPage == 3) currentPage = 4;
            if (currentPage == 5) currentPage = 6;
        }
        else if (circleindex == 1) // PREV
        {
            if (currentPage == 4) currentPage = 3;
            if (currentPage == 6) currentPage = 5;
        }
        else if (circleindex == 2) // EXIT APP
        {
            Application.Exit();
            System.Environment.Exit(0);
        }
        else if (circleindex == 3) // RESET
        {
            Random rnd = new Random();
            selectedSubject = rnd.Next(subjectOptions.Length);
            selectedVerb = rnd.Next(verbOptions.Length);
            selectedObject = rnd.Next(objectOptions.Length);

            answerChecked = false;
            isCorrect = false;
        }

        // CLOSE MENU AFTER CONFIRM
        circle = false;

        Invalidate();
    }
    private void Form_MouseClick(object sender, MouseEventArgs e)
    {

        //if (currentPage == 2)
        //{
        //
        //    int btnWidth = 450;
        //    int btnHeight = 450;
        //    int buttonX = 380;
        //    int buttonY = 550;
        //
        //
        //    if (e.X >= buttonX && e.X <= buttonX + btnWidth && e.Y >= buttonY && e.Y <= buttonY + btnHeight)
        //    {
        //        currentPage = 3;
        //        Invalidate();
        //    }
        //}


        // if (currentPage == 0)
        // {
        //     currentPage = 1; Invalidate();
        // }
        // else if (currentPage == 1)
        // {
        //     currentPage = 2; Invalidate();
        // }
        if (currentPage == 2)
        {
            currentPage = 10;
            Invalidate();
        }
        // else if (currentPage == 3)
        // {
        //     currentPage = 4; Invalidate();
        // }
        // else if (currentPage == 4)
        // {
        //     currentPage = 5; Invalidate();
        // }
        // else if (currentPage == 5)
        // {
        //     currentPage = 6; Invalidate();
        // }


    }



    private void Form_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyData == Keys.F1)
        {
            if (fullscreen == false)
            {
                width = screen_width;
                height = screen_height;

                window_left = this.Left;
                window_top = this.Top;

                this.FormBorderStyle = FormBorderStyle.None;
                this.Left = 0;
                this.Top = 0;
                this.Width = screen_width;
                this.Height = screen_height;

                fullscreen = true;
            }
            else
            {
                width = window_width;
                height = window_height;

                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.Left = window_left;
                this.Top = window_top;
                this.Width = window_width;
                this.Height = window_height;

                fullscreen = false;
            }
        }
        else if (e.KeyData == Keys.Escape)
        {
            this.Close();
        }
        else if (e.KeyData == Keys.S)
        {
            currentUser = "Guest";
            currentUserG = "m";   // change to "f" if you want girl mode
            currentPage = 2;
            feedbackMessage = "Login skipped. Welcome Guest.";
            Invalidate();
        }
        else if (e.KeyData == Keys.V)
        {
            verbose = !verbose;
        }
        // M key removed — mode switching now done via TUIO markers on mode selection screen


        if (currentPage == 7 || currentPage == 8)
        {
            if (e.KeyCode == Keys.Left) HandleGesture("LEFT");
            else if (e.KeyCode == Keys.Right) HandleGesture("RIGHT");
            else if (e.KeyCode == Keys.Up) HandleGesture("CIRCLE");
            else if (e.KeyCode == Keys.Space) HandleGesture("FIST");
        }
    }
    private void HandleGesture(string gestureName)
    {
        if (currentPage != 7 && currentPage != 8)
        {
            return;
        }
        if (gestureName == "LEFT")
        {
            MoveGestureLeft();
        }
        else if (gestureName == "RIGHT")
        {
            MoveGestureRight();
        }
        else if (gestureName == "CIRCLE")
        {
            SwapGestureSlot();
        }
        else if (gestureName == "FIST")
        {
            CheckGestureAnswer();
        }

        Invalidate();
    }

    private void HandleSentenceGesture(string gestureName)
    {
        if (currentPage != 3 && currentPage != 4 && currentPage != 5 && currentPage != 6)
        {
            return;
        }

        if (gestureName == "LEFT")
        {
            cameraGestureSlot--;

            if (cameraGestureSlot < 0)
                cameraGestureSlot = 2;

            answerChecked = false;
        }
        else if (gestureName == "RIGHT")
        {
            cameraGestureSlot++;

            if (cameraGestureSlot > 2)
                cameraGestureSlot = 0;

            answerChecked = false;
        }
        else if (gestureName == "CIRCLE")
        {
            if (cameraGestureSlot == 0)
            {
                selectedSubject++;
                if (selectedSubject >= subjectOptions.Length)
                    selectedSubject = 0;
            }
            else if (cameraGestureSlot == 1)
            {
                selectedVerb++;
                if (selectedVerb >= verbOptions.Length)
                    selectedVerb = 0;
            }
            else if (cameraGestureSlot == 2)
            {
                selectedObject++;
                if (selectedObject >= objectOptions.Length)
                    selectedObject = 0;
            }

            answerChecked = false;
        }
        else if (gestureName == "FIST")
        {
            CheckSentence();
        }

        Invalidate();
    }

    private void HandleLaserGesture(string gesture)
    {
        if (currentPage != 3 && currentPage != 5)
            return;

        if (gesture == "FIST")
        {
            // lock/unlock the current slot
            laserLocked = !laserLocked;
        }
        else if (gesture == "CIRCLE")
        {
            // cycle the word in the active slot
            if (laserSlot == 0)
            {
                selectedSubject++;
                if (selectedSubject >= subjectOptions.Length) selectedSubject = 0;
            }
            else if (laserSlot == 1)
            {
                selectedVerb++;
                if (selectedVerb >= verbOptions.Length) selectedVerb = 0;
            }
            else if (laserSlot == 2)
            {
                selectedObject++;
                if (selectedObject >= objectOptions.Length) selectedObject = 0;
            }
            answerChecked = false;
        }
        else if (gesture == "PEACE")
        {
            // submit answer
            CheckSentence();
        }

        cameraGestureSlot = laserSlot;
        Invalidate();
    }

    public bool connectGestureSocket(string host, int portNumber)
    {
        try
        {
            gestureSocketClient = new TcpClient(host, portNumber);
            gestureStream = gestureSocketClient.GetStream();
            Console.WriteLine("gesture connection made ! with " + host);
            return true;
        }
        catch (System.Net.Sockets.SocketException e)
        {
            Console.WriteLine("Gesture Connection Failed: " + e.Message);
            return false;
        }
    }

    public string recieveGestureMessage()
    {
        try
        {
            byte[] receiveBuffer = new byte[1024];
            int bytesReceived = gestureStream.Read(receiveBuffer, 0, 1024);
            Console.WriteLine("gesture bytes: " + bytesReceived);
            string data = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);
            Console.WriteLine("gesture data: " + data);
            return data;
        }
        catch (System.Exception e)
        {
            Console.WriteLine("Gesture Receive Error: " + e.Message);
        }

        return null;
    }

    public void streamGestureSocket()
    {
        connectGestureSocket("localhost", 5000);
        string msg = "";

        while (true)
        {
            msg = recieveGestureMessage();

            if (msg == null)
                break;

            msg = msg.Trim();

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    ProcessSocketMessage(msg);
                }));
            }
            else
            {
                ProcessSocketMessage(msg);
            }

            if (msg == "q")
            {
                gestureStream.Close();
                gestureSocketClient.Close();
                Console.WriteLine("Gesture Connection Terminated !");
                break;
            }
        }
    }
    private void MoveGestureLeft()
    {
        gestureSelectedSlot--;

        if (gestureSelectedSlot < 0)
            gestureSelectedSlot = 2;

        gestureAnswerChecked = false;
    }

    private void MoveGestureRight()
    {
        gestureSelectedSlot++;

        if (gestureSelectedSlot > 2)
            gestureSelectedSlot = 0;

        gestureAnswerChecked = false;
    }
    private void SwapGestureSlot()
    {
        if (gestureSelectedSlot == 0)
        {
            gestureSubject++;
            if (gestureSubject >= gestureSubjectOptions.Length)
                gestureSubject = 0;
        }
        else if (gestureSelectedSlot == 1)
        {
            gestureVerb++;
            if (gestureVerb >= gestureVerbOptions.Length)
                gestureVerb = 0;
        }
        else if (gestureSelectedSlot == 2)
        {
            gestureObject++;
            if (gestureObject >= gestureObjectOptions.Length)
                gestureObject = 0;
        }

        gestureAnswerChecked = false;
        Invalidate();
    }
    private void CheckGestureAnswer()
    {
        string sentence =
            gestureSubjectOptions[gestureSubject].ToUpper() + " " +
            gestureVerbOptions[gestureVerb].ToUpper() + " " +
            gestureObjectOptions[gestureObject].ToUpper();

        if (sentence == "I EAT APPLE")
            gestureIsCorrect = true;
        else
            gestureIsCorrect = false;

        gestureAnswerChecked = true;
    }

    private void Form_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        client.removeTuioListener(this);

        client.disconnect();
        System.Environment.Exit(0);
    }

    public void addTuioObject(TuioObject o)
    {
        lock (objectSync)
        {
            objectList.Add(o.SessionID, new TuioDemoObject(o));
        }

        if (currentPage == 0 && o.SymbolID == 0)
        {
            currentPage = 1;
            Invalidate();
            return;
        }


        if (currentPage == 2 && o.SymbolID == 7)
        {
            currentPage = 10;
            Invalidate();
            return;
        }
        if (currentPage == 2 && o.SymbolID == 6)
        {

            currentPage = 7;
            gestureMode = true;
            Invalidate();

        }
        if (currentPage == 3 || currentPage == 4 || currentPage == 5 || currentPage == 6)
        {
            if (o.SymbolID == 3)
            {
                CheckSentence();
                Invalidate();
            }

            if (o.SymbolID == 4)
            {
                circle = true;
            }

            if (o.SymbolID == 5)
            {
                if (circle)
                {
                    Circlemenu();
                }
            }



        }



        Invalidate();
    }



    public void updateTuioObject(TuioObject o)
    {
        lock (objectSync)
        {
            objectList[o.SessionID].update(o);
        }

        if (currentPage == 3 || currentPage == 4 || currentPage == 5 || currentPage == 6)
        {
            if (o.SymbolID == 0)
            {
                selectedSubject = GetRotationIndex(o.Angle, subjectOptions.Length);
            }
            else if (o.SymbolID == 1)
            {
                selectedVerb = GetRotationIndex(o.Angle, verbOptions.Length);
            }
            else if (o.SymbolID == 2)
            {
                selectedObject = GetRotationIndex(o.Angle, objectOptions.Length);
            }
            else if (o.SymbolID == 4)
            {

                circleindex = GetRotationIndex(o.Angle, circleOptions.Length);
            }
        }

        Invalidate();
    }
    public void removeTuioObject(TuioObject o)
    {
        lock (objectSync)
        {
            objectList.Remove(o.SessionID);
        }

        if (verbose) Console.WriteLine("del obj " + o.SymbolID + " (" + o.SessionID + ")");
    }

    public void addTuioCursor(TuioCursor c)
    {
        lock (cursorSync)
        {
            cursorList.Add(c.SessionID, c);
        }
        if (verbose) Console.WriteLine("add cur " + c.CursorID + " (" + c.SessionID + ") " + c.X + " " + c.Y);
    }

    public void updateTuioCursor(TuioCursor c)
    {
        if (verbose) Console.WriteLine("set cur " + c.CursorID + " (" + c.SessionID + ") " + c.X + " " + c.Y + " " + c.MotionSpeed + " " + c.MotionAccel);
    }

    public void removeTuioCursor(TuioCursor c)
    {
        lock (cursorSync)
        {
            cursorList.Remove(c.SessionID);
        }
        if (verbose) Console.WriteLine("del cur " + c.CursorID + " (" + c.SessionID + ")");
    }

    public void addTuioBlob(TuioBlob b)
    {
        lock (blobSync)
        {
            blobList.Add(b.SessionID, b);
        }
        if (verbose) Console.WriteLine("add blb " + b.BlobID + " (" + b.SessionID + ") " + b.X + " " + b.Y + " " + b.Angle + " " + b.Width + " " + b.Height + " " + b.Area);
    }

    public void updateTuioBlob(TuioBlob b)
    {
        if (verbose) Console.WriteLine("set blb " + b.BlobID + " (" + b.SessionID + ") " + b.X + " " + b.Y + " " + b.Angle + " " + b.Width + " " + b.Height + " " + b.Area + " " + b.MotionSpeed + " " + b.RotationSpeed + " " + b.MotionAccel + " " + b.RotationAccel);
    }

    public void removeTuioBlob(TuioBlob b)
    {
        lock (blobSync)
        {
            blobList.Remove(b.SessionID);
        }
        if (verbose) Console.WriteLine("del blb " + b.BlobID + " (" + b.SessionID + ")");
    }

    public void refresh(TuioTime frameTime)
    {
        Invalidate();
    }

    private void DrawGazeHighlight(Graphics g)
    {
        if (gazeSelectedSlot < 0)
        {
            return;
        }

        if (currentPage < 3 || currentPage > 8)
        {
            return;
        }

        int centerX = this.ClientSize.Width / 2;
        int frameW = 260;
        int frameH = 150;
        int spacing = 240;
        int totalW = (frameW * 3) + (spacing * 2);
        int startX = centerX - (totalW / 2);
        int frameY = 355;

        int selectedX = startX + (gazeSelectedSlot * (frameW + spacing));

        Rectangle highlightRect = new Rectangle(
            selectedX - 12,
            frameY - 12,
            frameW + 24,
            frameH + 24
        );

        using (Pen gazePen = new Pen(Color.Gold, 8))
        {
            g.DrawRectangle(gazePen, highlightRect);
        }

        using (Pen innerPen = new Pen(Color.Orange, 3))
        {
            g.DrawRectangle(
                innerPen,
                selectedX - 4,
                frameY - 4,
                frameW + 8,
                frameH + 8
            );
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;
        g.Clear(Color.White);

        Font titleFont = new Font("Arial", 26, FontStyle.Bold);
        Font normalFont = new Font("Arial", 16, FontStyle.Regular);
        Font bigFont = new Font("Arial", 20, FontStyle.Bold);
        Font displayFont = petitFont != null ? petitFont : bigFont;
        Font nameFont = montserratFont != null ? montserratFont : new Font("Arial", 30, FontStyle.Bold);

        //PAGE 0: Searching for Device
        if (currentPage == 0)
        {
            if (searchingfordevice != null)
                g.DrawImage(searchingfordevice, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            else
                g.DrawString("Searching for device...", titleFont, Brushes.Black, 220, 70);
        }

        //PAGE 1: Users List
        else if (currentPage == 1)
        {
            if (userslist != null)
                g.DrawImage(userslist, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            else
                g.DrawString("Users List", titleFont, Brushes.Black, 220, 70);
        }

        //PAGE 2: Welcome Back

        else if (currentPage == 2)
        {
            if (welcomeback != null)
                g.DrawImage(welcomeback, 0, 0, this.ClientSize.Width, this.ClientSize.Height);

            int btnWidth = 250;
            int btnHeight = 250;
            int buttonX = 355;
            int buttonY = 450;

            if (startBtn != null)
            {
                if (currentUser == "")
                {
                    g.DrawImage(startBtn, buttonX, buttonY, btnWidth, btnHeight);
                    string guestLevelText = totalLevels > 0 ? "  |  Level: 1/" + totalLevels : "";
                    g.DrawString("User: Guest" + guestLevelText, nameFont, Brushes.Black, 150, 450);
                }
                else
                {
                    g.DrawImage(startBtn, buttonX, buttonY, btnWidth, btnHeight);
                    string levelText = totalLevels > 0 ? "  |  Level: " + currentLevel + "/" + totalLevels : "";
                    g.DrawString("User: " + currentUser + levelText, nameFont, Brushes.Black, 150, 450);
                }

            }

            else
            {
                g.FillRectangle(Brushes.LightBlue, buttonX, buttonY, btnWidth, btnHeight);
                g.DrawString("START", normalFont, Brushes.Black, buttonX, buttonY);
            }
        }

        //Actual Game (Sentence Builder)
        else if (currentPage == 3)
        {
            if (boyBg != null)
                g.DrawImage(boyBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);

            int centerX = this.ClientSize.Width / 2;

            StringFormat centerAlign = new StringFormat();
            centerAlign.Alignment = StringAlignment.Center;
            centerAlign.LineAlignment = StringAlignment.Center;

            //   USERNAME IN YELLOW STICKY NOTE  
            int stickyX = this.ClientSize.Width - 350;
            int stickyY = 50;
            RectangleF stickyRect = new RectangleF(stickyX, stickyY, 320, 120);

            string displayName = string.IsNullOrEmpty(currentUser) ? "User: Guest  |  Lvl 1/" + totalLevels : "User: " + currentUser + "  |  Lvl " + currentLevel + "/" + totalLevels;
            g.DrawString(displayName, nameFont, Brushes.Black, stickyRect, centerAlign);

            //   Target Sentence Preview (Top White Box)  
            string subjText = subjectOptions[selectedSubject].ToUpper();
            string verbText = verbOptions[selectedVerb].ToUpper();
            string objText = objectOptions[selectedObject].ToUpper();

            SizeF subjSize = g.MeasureString(subjText, displayFont);
            SizeF verbSize = g.MeasureString(verbText, displayFont);
            SizeF objSize = g.MeasureString(objText, displayFont);

            int wordGap = 40;
            float totalSentenceWidth = subjSize.Width + wordGap + verbSize.Width + wordGap + objSize.Width;
            float sentenceStartX = centerX - (totalSentenceWidth / 2);
            float sentenceY = 110;

            g.DrawString(subjText, displayFont, subjectBrush, sentenceStartX, sentenceY);
            float verbX = sentenceStartX + subjSize.Width + wordGap;
            g.DrawString(verbText, displayFont, verbBrush, verbX, sentenceY);
            float objX = verbX + verbSize.Width + wordGap;
            g.DrawString(objText, displayFont, objectBrush, objX, sentenceY);

            //The 3 White Frames  
            int frameW = 260;
            int frameH = 150;
            int spacing = 240;
            int totalW = (frameW * 3) + (spacing * 2);
            int startX = centerX - (totalW / 2);
            int frameY = 355;

            Bitmap subjFrame = (cameraGestureSlot == 0 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap verbFrame = (cameraGestureSlot == 1 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap objFrame = (cameraGestureSlot == 2 && yellowFrame != null) ? yellowFrame : whiteFrame;

            if (whiteFrame != null)
            {
                g.DrawImage(subjFrame, startX, frameY, frameW, frameH);
                g.DrawImage(verbFrame, startX + frameW + spacing, frameY, frameW, frameH);
                g.DrawImage(objFrame, startX + (frameW + spacing) * 2, frameY, frameW, frameH);
            }

            int textOffsetY = 15;

            RectangleF subjRect = new RectangleF(startX, frameY + textOffsetY, frameW, frameH);
            g.DrawString(subjectOptions[selectedSubject].ToUpper(), displayFont, subjectBrush, subjRect, centerAlign);

            RectangleF verbRect = new RectangleF(startX + frameW + spacing, frameY + textOffsetY, frameW, frameH);
            g.DrawString(verbOptions[selectedVerb].ToUpper(), displayFont, verbBrush, verbRect, centerAlign);

            RectangleF objRect = new RectangleF(startX + (frameW + spacing) * 2, frameY + textOffsetY, frameW, frameH);
            g.DrawString(objectOptions[selectedObject].ToUpper(), displayFont, objectBrush, objRect, centerAlign);

            //Feedback text "CHECK YOUR ANSWER" box  
            int checkY = 680;
            RectangleF feedbackRect = new RectangleF(centerX - 320, checkY, 600, 100);

            if (answerChecked)
            {
                if (isCorrect)
                {
                    g.DrawString("Correct! Good Job!".ToUpper(), feedbackFont, verbBrush, feedbackRect, centerAlign);
                }
                else
                {
                    g.DrawString("Wrong! Try again!".ToUpper(), feedbackFont, subjectBrush, feedbackRect, centerAlign);
                }
            }
            if (circle)
            {
                int centerx = this.ClientSize.Width / 2;
                int centerY = this.ClientSize.Height / 2;
                int radius = 220;

                string[] labels = { "NEXT", "PREV", "EXIT", "RESET" };

                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float startAngle = i * 90f - 135f;
                        float sweepAngle = 90f;

                        bool disabled = false;
                        if (i == 0 && currentPage == 4) disabled = true;
                        if (i == 1 && currentPage == 3) disabled = true;


                        Brush brush;
                        if (disabled)
                            brush = Brushes.LightGray;
                        else if (i == circleindex)
                        {
                            brush = Brushes.Green;
                        }
                        else
                        {
                            brush = Brushes.Black;
                        }
                        g.FillPie(brush,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        g.DrawPie(Pens.White,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        double midAngle = (startAngle + sweepAngle / 2f) * Math.PI / 180.0;
                        int labelRadius = radius / 2;
                        int lx = centerx + (int)(Math.Cos(midAngle) * labelRadius);
                        int ly = centerY + (int)(Math.Sin(midAngle) * labelRadius);

                        StringFormat centeralign = new StringFormat();
                        centeralign.Alignment = StringAlignment.Center;
                        centeralign.LineAlignment = StringAlignment.Center;

                        RectangleF textRect = new RectangleF(lx - 60, ly - 20, 120, 40);
                        g.DrawString(labels[i], normalFont, Brushes.White, textRect, centeralign);
                    }
                }
            }
        }


        //PAGE 4: Actual Game (Sentence Builder)  
        else if (currentPage == 4)
        {
            if (boyBg != null)
                g.DrawImage(boyBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);

            int centerX = this.ClientSize.Width / 2;

            StringFormat centerAlign = new StringFormat();
            centerAlign.Alignment = StringAlignment.Center;
            centerAlign.LineAlignment = StringAlignment.Center;

            //   USERNAME IN YELLOW STICKY NOTE  
            int stickyX = this.ClientSize.Width - 350;
            int stickyY = 50;
            RectangleF stickyRect = new RectangleF(stickyX, stickyY, 320, 120);

            string displayName = string.IsNullOrEmpty(currentUser) ? "User: Guest  |  Lvl 1/" + totalLevels : "User: " + currentUser + "  |  Lvl " + currentLevel + "/" + totalLevels;
            g.DrawString(displayName, nameFont, Brushes.Black, stickyRect, centerAlign);

            //   Target Sentence Preview (Top White Box)  
            string subjText = subjectOptions[selectedSubject].ToUpper();
            string verbText = verbOptions[selectedVerb].ToUpper();
            string objText = objectOptions[selectedObject].ToUpper();

            SizeF subjSize = g.MeasureString(subjText, displayFont);
            SizeF verbSize = g.MeasureString(verbText, displayFont);
            SizeF objSize = g.MeasureString(objText, displayFont);

            int wordGap = 40;
            float totalSentenceWidth = subjSize.Width + wordGap + verbSize.Width + wordGap + objSize.Width;
            float sentenceStartX = centerX - (totalSentenceWidth / 2);
            float sentenceY = 110;


            g.DrawString(subjText, displayFont, subjectBrush, sentenceStartX, sentenceY);
            float verbX = sentenceStartX + subjSize.Width + wordGap;
            g.DrawString(verbText, displayFont, verbBrush, verbX, sentenceY);
            float objX = verbX + verbSize.Width + wordGap;
            g.DrawString(objText, displayFont, objectBrush, objX, sentenceY);

            //The 3 White Frames  
            int frameW = 260;
            int frameH = 150;
            int spacing = 240;
            int totalW = (frameW * 3) + (spacing * 2);
            int startX = centerX - (totalW / 2);
            int frameY = 355;

            Bitmap subjFrame = (cameraGestureSlot == 0 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap verbFrame = (cameraGestureSlot == 1 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap objFrame = (cameraGestureSlot == 2 && yellowFrame != null) ? yellowFrame : whiteFrame;

            if (whiteFrame != null)
            {
                g.DrawImage(subjFrame, startX, frameY, frameW, frameH);
                g.DrawImage(verbFrame, startX + frameW + spacing, frameY, frameW, frameH);
                g.DrawImage(objFrame, startX + (frameW + spacing) * 2, frameY, frameW, frameH);
            }

            int textOffsetY = 15;

            RectangleF subjRect = new RectangleF(startX, frameY + textOffsetY, frameW, frameH);
            g.DrawString(subjectOptions[selectedSubject].ToUpper(), displayFont, subjectBrush, subjRect, centerAlign);

            RectangleF verbRect = new RectangleF(startX + frameW + spacing, frameY + textOffsetY, frameW, frameH);
            g.DrawString(verbOptions[selectedVerb].ToUpper(), displayFont, verbBrush, verbRect, centerAlign);

            RectangleF objRect = new RectangleF(startX + (frameW + spacing) * 2, frameY + textOffsetY, frameW, frameH);
            g.DrawString(objectOptions[selectedObject].ToUpper(), displayFont, objectBrush, objRect, centerAlign);

            //Feedback text "CHECK YOUR ANSWER" box  
            int checkY = 680;
            RectangleF feedbackRect = new RectangleF(centerX - 320, checkY, 600, 100);

            if (answerChecked)
            {
                if (isCorrect)
                {
                    g.DrawString("Correct! Good Job!".ToUpper(), feedbackFont, verbBrush, feedbackRect, centerAlign);
                }
                else
                {
                    g.DrawString("Wrong! Try again!".ToUpper(), feedbackFont, subjectBrush, feedbackRect, centerAlign);
                }
            }

            if (circle)
            {
                int centerx = this.ClientSize.Width / 2;
                int centerY = this.ClientSize.Height / 2;
                int radius = 220;

                string[] labels = { "NEXT", "PREV", "EXIT", "RESET" };

                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float startAngle = i * 90f - 135f;
                        float sweepAngle = 90f;

                        bool disabled = false;
                        if (i == 0 && currentPage == 4) disabled = true;
                        if (i == 1 && currentPage == 3) disabled = true;

                        Brush brush;
                        if (disabled)
                            brush = Brushes.LightGray;
                        else if (i == circleindex)
                            brush = Brushes.Green;
                        else
                            brush = Brushes.Gray;

                        g.FillPie(brush,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        g.DrawPie(Pens.White,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        double midAngle = (startAngle + sweepAngle / 2f) * Math.PI / 180.0;
                        int labelRadius = radius / 2;
                        int lx = centerx + (int)(Math.Cos(midAngle) * labelRadius);
                        int ly = centerY + (int)(Math.Sin(midAngle) * labelRadius);

                        StringFormat centeralign = new StringFormat();
                        centeralign.Alignment = StringAlignment.Center;
                        centeralign.LineAlignment = StringAlignment.Center;

                        RectangleF textRect = new RectangleF(lx - 60, ly - 20, 120, 40);
                        g.DrawString(labels[i], normalFont, Brushes.White, textRect, centerAlign);
                    }
                }
            }
        }

        else if (currentPage == 5)
        {
            if (girlBg != null)
                g.DrawImage(girlBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);

            int centerX = this.ClientSize.Width / 2;

            StringFormat centerAlign = new StringFormat();
            centerAlign.Alignment = StringAlignment.Center;
            centerAlign.LineAlignment = StringAlignment.Center;

            //   USERNAME IN YELLOW STICKY NOTE  
            int stickyX = this.ClientSize.Width - 350;
            int stickyY = 50;
            RectangleF stickyRect = new RectangleF(stickyX, stickyY, 320, 120);

            string displayName = string.IsNullOrEmpty(currentUser) ? "User: Guest  |  Lvl 1/" + totalLevels : "User: " + currentUser + "  |  Lvl " + currentLevel + "/" + totalLevels;
            g.DrawString(displayName, nameFont, Brushes.Black, stickyRect, centerAlign);

            //   Target Sentence Preview (Top White Box)  
            string subjText = subjectOptions[selectedSubject].ToUpper();
            string verbText = verbOptions[selectedVerb].ToUpper();
            string objText = objectOptions[selectedObject].ToUpper();

            SizeF subjSize = g.MeasureString(subjText, displayFont);
            SizeF verbSize = g.MeasureString(verbText, displayFont);
            SizeF objSize = g.MeasureString(objText, displayFont);

            int wordGap = 40;
            float totalSentenceWidth = subjSize.Width + wordGap + verbSize.Width + wordGap + objSize.Width;
            float sentenceStartX = centerX - (totalSentenceWidth / 2);
            float sentenceY = 110;


            g.DrawString(subjText, displayFont, subjectBrush, sentenceStartX, sentenceY);
            float verbX = sentenceStartX + subjSize.Width + wordGap;
            g.DrawString(verbText, displayFont, verbBrush, verbX, sentenceY);
            float objX = verbX + verbSize.Width + wordGap;
            g.DrawString(objText, displayFont, objectBrush, objX, sentenceY);

            //The 3 White Frames  
            int frameW = 260;
            int frameH = 150;
            int spacing = 240;
            int totalW = (frameW * 3) + (spacing * 2);
            int startX = centerX - (totalW / 2);
            int frameY = 355;

            Bitmap subjFrame = (cameraGestureSlot == 0 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap verbFrame = (cameraGestureSlot == 1 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap objFrame = (cameraGestureSlot == 2 && yellowFrame != null) ? yellowFrame : whiteFrame;

            if (whiteFrame != null)
            {
                g.DrawImage(subjFrame, startX, frameY, frameW, frameH);
                g.DrawImage(verbFrame, startX + frameW + spacing, frameY, frameW, frameH);
                g.DrawImage(objFrame, startX + (frameW + spacing) * 2, frameY, frameW, frameH);
            }

            int textOffsetY = 15;

            RectangleF subjRect = new RectangleF(startX, frameY + textOffsetY, frameW, frameH);
            g.DrawString(subjectOptions[selectedSubject].ToUpper(), displayFont, subjectBrush, subjRect, centerAlign);

            RectangleF verbRect = new RectangleF(startX + frameW + spacing, frameY + textOffsetY, frameW, frameH);
            g.DrawString(verbOptions[selectedVerb].ToUpper(), displayFont, verbBrush, verbRect, centerAlign);

            RectangleF objRect = new RectangleF(startX + (frameW + spacing) * 2, frameY + textOffsetY, frameW, frameH);
            g.DrawString(objectOptions[selectedObject].ToUpper(), displayFont, objectBrush, objRect, centerAlign);

            //Feedback text "CHECK YOUR ANSWER" box  
            int checkY = 680;
            RectangleF feedbackRect = new RectangleF(centerX - 320, checkY, 600, 100);

            if (answerChecked)
            {
                if (isCorrect)
                {
                    g.DrawString("Correct! Good Job!".ToUpper(), feedbackFont, verbBrush, feedbackRect, centerAlign);
                }
                else
                {
                    g.DrawString("Wrong! Try again!".ToUpper(), feedbackFont, subjectBrush, feedbackRect, centerAlign);
                }
            }
            if (circle)
            {
                int centerx = this.ClientSize.Width / 2;
                int centerY = this.ClientSize.Height / 2;
                int radius = 220;

                string[] labels = { "NEXT", "PREV", "EXIT", "RESET" };

                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float startAngle = i * 90f - 135f;
                        float sweepAngle = 90f;

                        bool disabled = false;
                        if (i == 0 && currentPage == 6) disabled = true;
                        if (i == 1 && currentPage == 5) disabled = true;

                        Brush brush;
                        if (disabled)
                            brush = Brushes.LightGray;
                        else if (i == circleindex)
                            brush = Brushes.Green;
                        else
                            brush = Brushes.Gray;

                        g.FillPie(brush,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        g.DrawPie(Pens.White,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        double midAngle = (startAngle + sweepAngle / 2f) * Math.PI / 180.0;
                        int labelRadius = radius / 2;
                        int lx = centerx + (int)(Math.Cos(midAngle) * labelRadius);
                        int ly = centerY + (int)(Math.Sin(midAngle) * labelRadius);

                        StringFormat centeralign = new StringFormat();
                        centeralign.Alignment = StringAlignment.Center;
                        centeralign.LineAlignment = StringAlignment.Center;

                        RectangleF textRect = new RectangleF(lx - 60, ly - 20, 120, 40);
                        g.DrawString(labels[i], normalFont, Brushes.White, textRect, centeralign);
                    }
                }
            }
        }


        //PAGE 4: Actual Game (Sentence Builder)  
        else if (currentPage == 6)
        {
            if (girlBg != null)
                g.DrawImage(girlBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);

            int centerX = this.ClientSize.Width / 2;

            StringFormat centerAlign = new StringFormat();
            centerAlign.Alignment = StringAlignment.Center;
            centerAlign.LineAlignment = StringAlignment.Center;

            //   USERNAME IN YELLOW STICKY NOTE  
            int stickyX = this.ClientSize.Width - 350;
            int stickyY = 50;
            RectangleF stickyRect = new RectangleF(stickyX, stickyY, 320, 120);

            string displayName = string.IsNullOrEmpty(currentUser) ? "User: Guest  |  Lvl 1/" + totalLevels : "User: " + currentUser + "  |  Lvl " + currentLevel + "/" + totalLevels;
            g.DrawString(displayName, nameFont, Brushes.Black, stickyRect, centerAlign);

            //   Target Sentence Preview (Top White Box)  
            string subjText = subjectOptions[selectedSubject].ToUpper();
            string verbText = verbOptions[selectedVerb].ToUpper();
            string objText = objectOptions[selectedObject].ToUpper();

            SizeF subjSize = g.MeasureString(subjText, displayFont);
            SizeF verbSize = g.MeasureString(verbText, displayFont);
            SizeF objSize = g.MeasureString(objText, displayFont);

            int wordGap = 40;
            float totalSentenceWidth = subjSize.Width + wordGap + verbSize.Width + wordGap + objSize.Width;
            float sentenceStartX = centerX - (totalSentenceWidth / 2);
            float sentenceY = 110;


            g.DrawString(subjText, displayFont, subjectBrush, sentenceStartX, sentenceY);
            float verbX = sentenceStartX + subjSize.Width + wordGap;
            g.DrawString(verbText, displayFont, verbBrush, verbX, sentenceY);
            float objX = verbX + verbSize.Width + wordGap;
            g.DrawString(objText, displayFont, objectBrush, objX, sentenceY);

            //The 3 White Frames  
            int frameW = 260;
            int frameH = 150;
            int spacing = 240;
            int totalW = (frameW * 3) + (spacing * 2);
            int startX = centerX - (totalW / 2);
            int frameY = 355;

            Bitmap subjFrame = (cameraGestureSlot == 0 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap verbFrame = (cameraGestureSlot == 1 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap objFrame = (cameraGestureSlot == 2 && yellowFrame != null) ? yellowFrame : whiteFrame;

            if (whiteFrame != null)
            {
                g.DrawImage(subjFrame, startX, frameY, frameW, frameH);
                g.DrawImage(verbFrame, startX + frameW + spacing, frameY, frameW, frameH);
                g.DrawImage(objFrame, startX + (frameW + spacing) * 2, frameY, frameW, frameH);
            }

            int textOffsetY = 15;

            RectangleF subjRect = new RectangleF(startX, frameY + textOffsetY, frameW, frameH);
            g.DrawString(subjectOptions[selectedSubject].ToUpper(), displayFont, subjectBrush, subjRect, centerAlign);

            RectangleF verbRect = new RectangleF(startX + frameW + spacing, frameY + textOffsetY, frameW, frameH);
            g.DrawString(verbOptions[selectedVerb].ToUpper(), displayFont, verbBrush, verbRect, centerAlign);

            RectangleF objRect = new RectangleF(startX + (frameW + spacing) * 2, frameY + textOffsetY, frameW, frameH);
            g.DrawString(objectOptions[selectedObject].ToUpper(), displayFont, objectBrush, objRect, centerAlign);

            //Feedback text "CHECK YOUR ANSWER" box  
            int checkY = 680;
            RectangleF feedbackRect = new RectangleF(centerX - 320, checkY, 600, 100);

            if (answerChecked)
            {
                if (isCorrect)
                {
                    g.DrawString("Correct! Good Job!".ToUpper(), feedbackFont, verbBrush, feedbackRect, centerAlign);
                }
                else
                {
                    g.DrawString("Wrong! Try again!".ToUpper(), feedbackFont, subjectBrush, feedbackRect, centerAlign);
                }
            }

            if (circle)
            {
                int centerx = this.ClientSize.Width / 2;
                int centerY = this.ClientSize.Height / 2;
                int radius = 220;

                string[] labels = { "NEXT", "PREV", "EXIT", "RESET" };

                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float startAngle = i * 90f - 135f;
                        float sweepAngle = 90f;

                        bool disabled = false;
                        if (i == 0 && currentPage == 6) disabled = true;
                        if (i == 1 && currentPage == 5) disabled = true;

                        Brush brush;
                        if (disabled)
                            brush = Brushes.LightGray;
                        else if (i == circleindex)
                            brush = Brushes.Green;
                        else
                            brush = Brushes.Gray;

                        g.FillPie(brush,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        g.DrawPie(Pens.White,
                            centerx - radius, centerY - radius,
                            radius * 2, radius * 2,
                            startAngle, sweepAngle);

                        double midAngle = (startAngle + sweepAngle / 2f) * Math.PI / 180.0;
                        int labelRadius = radius / 2;
                        int lx = centerx + (int)(Math.Cos(midAngle) * labelRadius);
                        int ly = centerY + (int)(Math.Sin(midAngle) * labelRadius);

                        StringFormat centeralign = new StringFormat();
                        centeralign.Alignment = StringAlignment.Center;
                        centeralign.LineAlignment = StringAlignment.Center;

                        RectangleF textRect = new RectangleF(lx - 60, ly - 20, 120, 40);
                        g.DrawString(labels[i], normalFont, Brushes.White, textRect, centerAlign);
                    }
                }
            }
        }

        else if (currentPage == 7)
        {
            if (boyBg != null)
                g.DrawImage(boyBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            else
                g.Clear(Color.White);

            int centerX = this.ClientSize.Width / 2;

            StringFormat centerAlign = new StringFormat();
            centerAlign.Alignment = StringAlignment.Center;
            centerAlign.LineAlignment = StringAlignment.Center;

            Brush selectedBrush = Brushes.DeepPink;
            Brush normalBrush = Brushes.Black;

            int stickyX = this.ClientSize.Width - 350;
            int stickyY = 50;
            RectangleF stickyRect = new RectangleF(stickyX, stickyY, 320, 120);

            string displayName = string.IsNullOrEmpty(currentUser) ? "User: Guest  |  Lvl 1/" + totalLevels : "User: " + currentUser + "  |  Lvl " + currentLevel + "/" + totalLevels;
            g.DrawString(displayName, nameFont, Brushes.Black, stickyRect, centerAlign);

            string subjText = gestureSubjectOptions[gestureSubject].ToUpper();
            string verbText = gestureVerbOptions[gestureVerb].ToUpper();
            string objText = gestureObjectOptions[gestureObject].ToUpper();

            SizeF subjSize = g.MeasureString(subjText, displayFont);
            SizeF verbSize = g.MeasureString(verbText, displayFont);
            SizeF objSize = g.MeasureString(objText, displayFont);

            int wordGap = 40;
            float totalSentenceWidth = subjSize.Width + wordGap + verbSize.Width + wordGap + objSize.Width;
            float sentenceStartX = centerX - (totalSentenceWidth / 2);
            float sentenceY = 110;

            Brush topSubjBrush = gestureSelectedSlot == 0 ? selectedBrush : normalBrush;
            Brush topVerbBrush = gestureSelectedSlot == 1 ? selectedBrush : normalBrush;
            Brush topObjBrush = gestureSelectedSlot == 2 ? selectedBrush : normalBrush;

            g.DrawString(subjText, displayFont, topSubjBrush, sentenceStartX, sentenceY);

            float verbX = sentenceStartX + subjSize.Width + wordGap;
            g.DrawString(verbText, displayFont, topVerbBrush, verbX, sentenceY);

            float objX = verbX + verbSize.Width + wordGap;
            g.DrawString(objText, displayFont, topObjBrush, objX, sentenceY);

            int frameW = 260;
            int frameH = 150;
            int spacing = 240;
            int totalW = (frameW * 3) + (spacing * 2);
            int startX = centerX - (totalW / 2);
            int frameY = 355;

            Bitmap gSubjFrame = (gestureSelectedSlot == 0 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap gVerbFrame = (gestureSelectedSlot == 1 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap gObjFrame = (gestureSelectedSlot == 2 && yellowFrame != null) ? yellowFrame : whiteFrame;

            if (whiteFrame != null)
            {
                g.DrawImage(gSubjFrame, startX, frameY, frameW, frameH);
                g.DrawImage(gVerbFrame, startX + frameW + spacing, frameY, frameW, frameH);
                g.DrawImage(gObjFrame, startX + (frameW + spacing) * 2, frameY, frameW, frameH);
            }

            int textOffsetY = 15;

            Brush frameSubjBrush = gestureSelectedSlot == 0 ? selectedBrush : normalBrush;
            Brush frameVerbBrush = gestureSelectedSlot == 1 ? selectedBrush : normalBrush;
            Brush frameObjBrush = gestureSelectedSlot == 2 ? selectedBrush : normalBrush;

            RectangleF subjRect = new RectangleF(startX, frameY + textOffsetY, frameW, frameH);
            g.DrawString(gestureSubjectOptions[gestureSubject].ToUpper(), displayFont, frameSubjBrush, subjRect, centerAlign);

            RectangleF verbRect = new RectangleF(startX + frameW + spacing, frameY + textOffsetY, frameW, frameH);
            g.DrawString(gestureVerbOptions[gestureVerb].ToUpper(), displayFont, frameVerbBrush, verbRect, centerAlign);

            RectangleF objRect = new RectangleF(startX + (frameW + spacing) * 2, frameY + textOffsetY, frameW, frameH);
            g.DrawString(gestureObjectOptions[gestureObject].ToUpper(), displayFont, frameObjBrush, objRect, centerAlign);

            RectangleF helpRect = new RectangleF(centerX - 450, 580, 900, 60);
            g.DrawString("LEFT / RIGHT = MOVE   |   CIRCLE = SWAP   |   FIST = CHECK", normalFont, Brushes.Black, helpRect, centerAlign);

            int checkY = 680;
            RectangleF feedbackRect = new RectangleF(centerX - 320, checkY, 600, 100);

            if (gestureAnswerChecked)
            {
                if (gestureIsCorrect)
                    g.DrawString("CORRECT! GOOD JOB!", feedbackFont, Brushes.Green, feedbackRect, centerAlign);
                else
                    g.DrawString("WRONG! TRY AGAIN!", feedbackFont, Brushes.Red, feedbackRect, centerAlign);
            }
        }

        else if (currentPage == 8)
        {
            if (girlBg != null)
                g.DrawImage(girlBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            else
                g.Clear(Color.White);

            int centerX = this.ClientSize.Width / 2;

            StringFormat centerAlign = new StringFormat();
            centerAlign.Alignment = StringAlignment.Center;
            centerAlign.LineAlignment = StringAlignment.Center;

            Brush selectedBrush = Brushes.DeepPink;
            Brush normalBrush = Brushes.Black;

            int stickyX = this.ClientSize.Width - 350;
            int stickyY = 50;
            RectangleF stickyRect = new RectangleF(stickyX, stickyY, 320, 120);

            string displayName = string.IsNullOrEmpty(currentUser) ? "User: Guest  |  Lvl 1/" + totalLevels : "User: " + currentUser + "  |  Lvl " + currentLevel + "/" + totalLevels;
            g.DrawString(displayName, nameFont, Brushes.Black, stickyRect, centerAlign);

            string subjText = gestureSubjectOptions[gestureSubject].ToUpper();
            string verbText = gestureVerbOptions[gestureVerb].ToUpper();
            string objText = gestureObjectOptions[gestureObject].ToUpper();

            SizeF subjSize = g.MeasureString(subjText, displayFont);
            SizeF verbSize = g.MeasureString(verbText, displayFont);
            SizeF objSize = g.MeasureString(objText, displayFont);

            int wordGap = 40;
            float totalSentenceWidth = subjSize.Width + wordGap + verbSize.Width + wordGap + objSize.Width;
            float sentenceStartX = centerX - (totalSentenceWidth / 2);
            float sentenceY = 110;

            Brush topSubjBrush = gestureSelectedSlot == 0 ? selectedBrush : normalBrush;
            Brush topVerbBrush = gestureSelectedSlot == 1 ? selectedBrush : normalBrush;
            Brush topObjBrush = gestureSelectedSlot == 2 ? selectedBrush : normalBrush;

            g.DrawString(subjText, displayFont, topSubjBrush, sentenceStartX, sentenceY);

            float verbX = sentenceStartX + subjSize.Width + wordGap;
            g.DrawString(verbText, displayFont, topVerbBrush, verbX, sentenceY);

            float objX = verbX + verbSize.Width + wordGap;
            g.DrawString(objText, displayFont, topObjBrush, objX, sentenceY);

            int frameW = 260;
            int frameH = 150;
            int spacing = 240;
            int totalW = (frameW * 3) + (spacing * 2);
            int startX = centerX - (totalW / 2);
            int frameY = 355;

            Bitmap gSubjFrame = (gestureSelectedSlot == 0 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap gVerbFrame = (gestureSelectedSlot == 1 && yellowFrame != null) ? yellowFrame : whiteFrame;
            Bitmap gObjFrame = (gestureSelectedSlot == 2 && yellowFrame != null) ? yellowFrame : whiteFrame;

            if (whiteFrame != null)
            {
                g.DrawImage(gSubjFrame, startX, frameY, frameW, frameH);
                g.DrawImage(gVerbFrame, startX + frameW + spacing, frameY, frameW, frameH);
                g.DrawImage(gObjFrame, startX + (frameW + spacing) * 2, frameY, frameW, frameH);
            }

            int textOffsetY = 15;

            Brush frameSubjBrush = gestureSelectedSlot == 0 ? selectedBrush : normalBrush;
            Brush frameVerbBrush = gestureSelectedSlot == 1 ? selectedBrush : normalBrush;
            Brush frameObjBrush = gestureSelectedSlot == 2 ? selectedBrush : normalBrush;

            RectangleF subjRect = new RectangleF(startX, frameY + textOffsetY, frameW, frameH);
            g.DrawString(gestureSubjectOptions[gestureSubject].ToUpper(), displayFont, frameSubjBrush, subjRect, centerAlign);

            RectangleF verbRect = new RectangleF(startX + frameW + spacing, frameY + textOffsetY, frameW, frameH);
            g.DrawString(gestureVerbOptions[gestureVerb].ToUpper(), displayFont, frameVerbBrush, verbRect, centerAlign);

            RectangleF objRect = new RectangleF(startX + (frameW + spacing) * 2, frameY + textOffsetY, frameW, frameH);
            g.DrawString(gestureObjectOptions[gestureObject].ToUpper(), displayFont, frameObjBrush, objRect, centerAlign);

            RectangleF helpRect = new RectangleF(centerX - 450, 580, 900, 60);
            g.DrawString("LEFT / RIGHT = MOVE   |   CIRCLE = SWAP   |   FIST = CHECK", normalFont, Brushes.Black, helpRect, centerAlign);

            int checkY = 680;
            RectangleF feedbackRect = new RectangleF(centerX - 320, checkY, 600, 100);

            if (gestureAnswerChecked)
            {
                if (gestureIsCorrect)
                    g.DrawString("CORRECT! GOOD JOB!", feedbackFont, Brushes.Green, feedbackRect, centerAlign);
                else
                    g.DrawString("WRONG! TRY AGAIN!", feedbackFont, Brushes.Red, feedbackRect, centerAlign);
            }
        }

        // Gaze-based adaptive highlight overlay
        DrawGazeHighlight(g);

        // Separate adaptive feedback overlays:
        // 1) Emotion feedback box
        // 2) Gaze hint box
        if (currentPage >= 3 && currentPage <= 8)
        {
            using (StringFormat centerText = new StringFormat())
            {
                centerText.Alignment = StringAlignment.Center;
                centerText.LineAlignment = StringAlignment.Center;

                if (emotionMessage != "")
                {
                    RectangleF emotionRect = new RectangleF(
                        this.ClientSize.Width / 2 - 450,
                        this.ClientSize.Height - 175,
                        900,
                        60
                    );

                    using (SolidBrush emotionBrush = new SolidBrush(Color.FromArgb(205, Color.White)))
                    {
                        g.FillRectangle(emotionBrush, emotionRect);
                    }

                    using (Pen emotionPen = new Pen(Color.MediumPurple, 3))
                    {
                        g.DrawRectangle(
                            emotionPen,
                            emotionRect.X,
                            emotionRect.Y,
                            emotionRect.Width,
                            emotionRect.Height
                        );
                    }

                    g.DrawString(
                        "Emotion: " + emotionMessage,
                        normalFont,
                        Brushes.Black,
                        emotionRect,
                        centerText
                    );
                }

                if (gazeHintMessage != "")
                {
                    RectangleF gazeRect = new RectangleF(
                        this.ClientSize.Width / 2 - 450,
                        this.ClientSize.Height - 105,
                        900,
                        60
                    );

                    using (SolidBrush gazeBrush = new SolidBrush(Color.FromArgb(215, Color.White)))
                    {
                        g.FillRectangle(gazeBrush, gazeRect);
                    }

                    using (Pen gazePen = new Pen(Color.Goldenrod, 3))
                    {
                        g.DrawRectangle(
                            gazePen,
                            gazeRect.X,
                            gazeRect.Y,
                            gazeRect.Width,
                            gazeRect.Height
                        );
                    }

                    g.DrawString(
                        "Gaze Hint: " + gazeHintMessage,
                        normalFont,
                        Brushes.Black,
                        gazeRect,
                        centerText
                    );
                }
            }
        }

        // mode selection screen (page 10)
        if (currentPage == 10)
        {
            try
            {
                if (selectBg != null)
                    g.DrawImage(selectBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
                else if (boyBg != null)
                    g.DrawImage(boyBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
                else
                    g.Clear(Color.SkyBlue);
            }
            catch { g.Clear(Color.SkyBlue); }

            // text goes in the white box on the left side of select.png
            // white box is roughly x=220 to x=680, y=120 to y=700
            StringFormat center = new StringFormat();
            center.Alignment = StringAlignment.Center;
            center.LineAlignment = StringAlignment.Center;

            int boxX = 220;
            int boxW = 460;

            g.DrawString("Choose Your Mode", feedbackFont, Brushes.DeepPink,
                new RectangleF(boxX, 150, boxW, 80), center);

            g.DrawString("Circle Gesture", nameFont, Brushes.Black,
                new RectangleF(boxX, 290, boxW, 50), center);
            g.DrawString("Gesture Mode", montserratFont, Brushes.DarkGreen,
                new RectangleF(boxX, 335, boxW, 50), center);

            g.DrawString("Fist Gesture", nameFont, Brushes.Black,
                new RectangleF(boxX, 430, boxW, 50), center);
            g.DrawString("Laser Mode", montserratFont, Brushes.DarkBlue,
                new RectangleF(boxX, 475, boxW, 50), center);
        }

        // win screen on top when all levels done
        if (showWinScreen)
        {
            if (winBg != null)
                g.DrawImage(winBg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            else
            {
                using (SolidBrush goldBrush = new SolidBrush(Color.FromArgb(220, Color.Gold)))
                    g.FillRectangle(goldBrush, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }

            StringFormat winFmt = new StringFormat();
            winFmt.Alignment = StringAlignment.Center;
            winFmt.LineAlignment = StringAlignment.Center;
            RectangleF winRect = new RectangleF(0, this.ClientSize.Height / 2 - 80, this.ClientSize.Width, 160);
            g.DrawString("YOU WIN!", feedbackFont, Brushes.White, winRect, winFmt);
        }

        // mode label bottom left
        if (currentPage == 3 || currentPage == 5)
        {
            string modeText = laserMode
                ? (laserLocked ? "MODE: LASER  [LOCKED]" : "MODE: LASER  [UNLOCKED]")
                : "MODE: GESTURE";

            using (SolidBrush modeBg = new SolidBrush(laserMode ? Color.FromArgb(200, Color.DarkBlue) : Color.FromArgb(200, Color.DarkGreen)))
                g.FillRectangle(modeBg, 10, this.ClientSize.Height - 55, 340, 42);

            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                g.DrawString(modeText, normalFont, Brushes.White, new RectangleF(18, this.ClientSize.Height - 55, 330, 42), sf);
        }

        // laser pointer dot — only in laser mode, scale from camera to screen coords
        if (laserMode && fingerDotX >= 0 && fingerDotY >= 0 && (currentPage == 3 || currentPage == 5))
        {
            int camWidth = 640;
            int camHeight = 480;
            int screenDotX = (int)((float)fingerDotX / camWidth * this.ClientSize.Width);
            int screenDotY = (int)((float)fingerDotY / camHeight * this.ClientSize.Height);
            int dotSize = 24;
            g.FillEllipse(Brushes.Lime, screenDotX - dotSize / 2, screenDotY - dotSize / 2, dotSize, dotSize);
            g.DrawEllipse(new Pen(Color.DarkGreen, 2), screenDotX - dotSize / 2, screenDotY - dotSize / 2, dotSize, dotSize);
        }

    }

    public static void Main(String[] argv)
    {
        int port = 0;
        switch (argv.Length)
        {
            case 1:
                port = int.Parse(argv[0], null);
                if (port == 0) goto default;
                break;
            case 0:
                port = 3333;
                break;
            default:
                Console.WriteLine("usage: java TuioDemo [port]");
                System.Environment.Exit(0);
                break;
        }

        TuioDemo app = new TuioDemo(port);
        Application.Run(app);
    }
}