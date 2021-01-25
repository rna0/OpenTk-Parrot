#region License

//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2009 the Open Toolkit library.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to 
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//

#endregion

using System;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace Parrot
{
    public partial class Form1 : Form
    {
        #region Fields

        private AudioContext AudioContext;
        private AudioCapture AudioCapture;

        private int Src;
        private short[] Buffer = new short[512];
        private const byte SAMPLE_TO_BYTE = 2;

        #endregion

        #region Constructors

        public Form1()
        {
            InitializeComponent();

            Text = "OpenAL Parrot (" + (IntPtr.Size == 4 ? "x86" : "x64") + ")";

            // Add available capture devices to the combo box.
            var recorders = AudioCapture.AvailableDevices;
            foreach (var t in recorders)
            {
                if (!string.IsNullOrEmpty(t))
                    comboBox_RecorderSelection.Items.Add(t);
            }

            if (comboBox_RecorderSelection.Items.Count > 0)
                comboBox_RecorderSelection.SelectedIndex = 0;
        }

        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        #endregion

        #region Events

        private void button_Start_Click(object sender, EventArgs e)
        {
            if (AudioCapture == null || !AudioCapture.IsRunning)
            {
                button_Start.Text = "Stop Recording";
                groupBox_RecorderParameters.Enabled = false;
                this.StartRecording();
            }
            else
            {
                button_Start.Text = "Start Recording";
                groupBox_RecorderParameters.Enabled = true;
                this.StopRecording();
            }
        }

        private void timer_UpdateSamples_Tick(object sender, EventArgs e)
        {
            this.UpdateSamples();
        }

        private void Parrot_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.StopRecording();
        }

        #endregion

        #region Private Members

        void StartRecording()
        {
            try
            {
                AudioContext = new AudioContext();
            }
            catch (AudioException ae)
            {
                MessageBox.Show(
                    "Fatal: Cannot continue without a playback device.\nException caught when opening playback device.\n" +
                    ae.Message);
                Application.Exit();
            }

            AL.Listener(ALListenerf.Gain, (float) numericUpDown_PlaybackGain.Value);
            Src = AL.GenSource();

            var samplingRate = (int) numericUpDown_Frequency.Value;
            var bufferLengthMs = (double) numericUpDown_BufferLength.Value;
            var bufferLengthSamples = (int) ((double) numericUpDown_BufferLength.Value * samplingRate * 0.001 /
                                             BlittableValueType.StrideOf(Buffer));

            try
            {
                AudioCapture = new AudioCapture((string) comboBox_RecorderSelection.SelectedItem,
                    samplingRate, ALFormat.Mono16, bufferLengthSamples);
            }
            catch (AudioDeviceException ade)
            {
                MessageBox.Show("Exception caught when opening recording device.\n" + ade.Message);
                AudioCapture = null;
            }

            if (AudioCapture == null)
                return;

            AudioCapture.Start();

            timer_GetSamples.Start();
            timer_GetSamples.Interval = (int) (bufferLengthMs / 2 + 0.5); // Tick when half the buffer is full.
        }

        void StopRecording()
        {
            timer_GetSamples.Stop();

            if (AudioCapture != null)
            {
                AudioCapture.Stop();
                AudioCapture.Dispose();
                AudioCapture = null;
            }

            if (AudioContext != null)
            {
                AL.GetSource(Src, ALGetSourcei.BuffersQueued, out var r);
                ClearBuffers(r);

                AL.DeleteSource(Src);

                AudioContext.Dispose();
                AudioContext = null;
            }
        }

        private void UpdateSamples()
        {
            if (AudioCapture == null)
                return;

            var availableSamples = AudioCapture.AvailableSamples;

            if (availableSamples * SAMPLE_TO_BYTE > Buffer.Length * BlittableValueType.StrideOf(Buffer))
            {
                Buffer = new short[MathHelper.NextPowerOfTwo(
                    (int) (availableSamples * SAMPLE_TO_BYTE / (double) BlittableValueType.StrideOf(Buffer) + 0.5))];
            }

            if (availableSamples > 0)
            {
                AudioCapture.ReadSamples(Buffer, availableSamples);

                var buf = AL.GenBuffer();
                AL.BufferData(buf, ALFormat.Mono16, Buffer,
                    availableSamples * BlittableValueType.StrideOf(Buffer), AudioCapture.SampleFrequency);
                AL.SourceQueueBuffer(Src, buf);

                label_SamplesConsumed.Text = "Samples consumed: " + availableSamples;

                if (AL.GetSourceState(Src) != ALSourceState.Playing)
                    AL.SourcePlay(Src);
            }

            ClearBuffers(0);
        }

        void ClearBuffers(int input)
        {
            if (AudioContext == null)
                return;

            int[] freedbuffers;
            if (input == 0)
            {
                AL.GetSource(Src, ALGetSourcei.BuffersProcessed, out var buffersProcessed);
                if (buffersProcessed == 0)
                    return;
                freedbuffers = AL.SourceUnqueueBuffers(Src, buffersProcessed);
            }
            else
            {
                freedbuffers = AL.SourceUnqueueBuffers(Src, input);
            }

            AL.DeleteBuffers(freedbuffers);
        }

        #endregion
    }
}