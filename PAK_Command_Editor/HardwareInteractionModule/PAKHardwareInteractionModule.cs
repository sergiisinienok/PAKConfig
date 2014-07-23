﻿using PAK_Command_Editor.CustomEventArgs;
using PAK_Command_Editor.Settings;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PAK_Command_Editor.HardwareInteractionModule
{
    public class PAKHardwareInteractionModule : IDisposable
    {
        public static readonly String DATA_SENT_SUCCESSFULLY_NO_WAIT = "Данные успешно отправлены в порт {0}";
        private static readonly String DATA_READ_TIMEOUT = "Истекло время ожидания ответа от порта {0}";
        private SerialPort _comPort;

        public event EventHandler<ComPortDataEventArgs> DataReceivedFromPort;

        public PAKHardwareInteractionModule()
        {
            this._comPort = new SerialPort(PAKSettingsManager.Settings.COMPortName, PAKSettingsManager.Settings.COMPortBandwidth);
        }

        public String SendData(String data)
        {
            try
            {
                if (this._comPort.IsOpen == false) //if not open, open the port
                    this._comPort.Open();             

                this._comPort.Write(data);           

                return String.Format(DATA_SENT_SUCCESSFULLY_NO_WAIT, PAKSettingsManager.Settings.COMPortName);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public String SendDataAndWait(String data)
        {
            try
            {
                if (this._comPort.IsOpen == false) //if not open, open the port
                    this._comPort.Open();

                this._comPort.DataReceived += _comPort_DataReceived;
                //send                  
                this._comPort.Write(data);               
                
                return String.Format(DATA_SENT_SUCCESSFULLY_NO_WAIT, PAKSettingsManager.Settings.COMPortName);
            }
            catch (Exception e)
            {
                return e.Message;
            }           
        }

        void _comPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {          
            //Initialize a buffer to hold the received data
            byte[] buffer = new byte[this._comPort.ReadBufferSize];

            //There is no accurate method for checking how many bytes are read
            //unless you check the return from the Read method
            int bytesRead = this._comPort.Read(buffer, 0, buffer.Length);

            if ((bytesRead > 0) && (this.DataReceivedFromPort != null))
            {
                ComPortDataEventArgs args = new ComPortDataEventArgs() { ReceivedData = buffer };
                this.DataReceivedFromPort(this, args);
            }

        }        

        #region Utilities

        private byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            try
            {
                this._comPort.DiscardInBuffer();
                this._comPort.DiscardOutBuffer();
                this._comPort.Close();
            }
            catch { }
            finally
            {
                this._comPort.Dispose();
            }
        }

        #endregion
    }
}