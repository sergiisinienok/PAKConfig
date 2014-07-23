﻿using NHibernate;
using PAK_Command_Editor.CustomEventArgs;
using PAK_Command_Editor.Entities;
using PAK_Command_Editor.HardwareInteractionModule;
using PAK_Command_Editor.Repository;
using PAK_Command_Editor.Settings;
using PAK_Command_Editor.SignalsCatalog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PAK_Command_Editor.MacrosEditor
{
    public partial class MacrosesEditorForm : Form
    {
        private static readonly String SELECT_TEXT = "Выберите...";
        private static readonly String ERROR = "Ошибка";
        private static readonly String DG_COMMAND = "Имя команды";
        private static readonly String DG_PARAMS = "Параметры";
        private static readonly String EMPTY_MACROS_MESSAGE = "Макрос не содержит команд. \r\nВоспользуйтесь кнопкой Добавить Команду.";
        private static readonly String MACROS_EXPORTED_SUCCESFULLY = "Текущий макрос успешно сохранен в файл \r\n{0}";
        private static readonly String SEND_PARAMS_TEMPLATE = "Сигнал: {0}, Адресат: {1}";
        private static readonly String CANCEL_WRN_CAPTION = "Закрытие редактора макросов";
        private static readonly String CANCEL_WARNING = "Все изменения будут потеряны. Закрыть редактор?";
        private ISession _dataSession;
        private IRepository<Signal> _signalsRepo;
        private IRepository<MacrosCommand> _macrosCommandRepo;
        private Int32 _rowIndex;
        private MacrosesContainer _macrosesContainer;
        private PAKHardwareInteractionModule _hwModule;

        private int _prevWidth;
        private FormWindowState _prevWindowState;

        public String MacrosFileName { get; set; }
        public Boolean UploadFromDeviceNeeded { get; set; }
        public event EventHandler MacrosReadyToSave;
        public event EventHandler MacrosChanged;

        public Boolean MacrosConfigured
        {
            get
            {
                return (this.cbSelectSignal.SelectedIndex > 0) && (this.gvMacros.RowCount > 0);
            }
        }

        public MacrosesEditorForm()
        {
            InitializeComponent();

            this._prevWidth = this.Width;
            this._prevWindowState = this.WindowState;
            this._hwModule = new PAKHardwareInteractionModule();
            this._dataSession = PAKDataSessionFactory.GetSession();
            this._signalsRepo = new Repository<Signal>(this._dataSession);
            this._macrosCommandRepo = new Repository<MacrosCommand>(this._dataSession);            
        }

        #region Buttons Event Handlers

        private void btnSaveMacros_Click(object sender, EventArgs e)
        {
            if (this.MacrosReadyToSave != null)
                this.MacrosReadyToSave(this, new EventArgs());
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnAddSignal_Click(object sender, EventArgs e)
        {
            AddSignalForm addSignalForm = new AddSignalForm();
            addSignalForm.SignalAdded += addSignalForm_SignalAdded;
            addSignalForm.ShowDialog();
        }

        private void btnAddCommand_Click(object sender, EventArgs e)
        {
            SelectCommandTypeForm selectCommandTypeForm = new SelectCommandTypeForm(this);
            selectCommandTypeForm.ShowDialog();
        }

        private void btnDeleteCommand_Click(object sender, EventArgs e)
        {
            this._macrosesContainer.Commands.RemoveAt(this._rowIndex);
            this.BindMacrosesGrid();
        }

        #endregion

        #region MacrosesEditorForm Event Handlers

        private void MacrosesEditorForm_Shown(object sender, EventArgs e)
        {
            if (this.UploadFromDeviceNeeded)
            {
                this._hwModule.DataReceivedFromPort += _hwModule_DataReceivedFromPort;
                this._hwModule.SendDataAndWait(PAKSettingsManager.Settings.ReadSignalCommand);
            }

            this.BindSignals();
            this.cbSelectSignal.SelectedIndex = 0;
            this.InitMacrosesContainer();
            this.BindMacrosesGrid();
        }        

        private void MacrosesEditorForm_Resize(object sender, EventArgs e)
        {
            if ((this.WindowState != this._prevWindowState) && (this.WindowState != FormWindowState.Minimized))
            {
                this._prevWindowState = this.WindowState;
                ResizeGrid(this.gvMacros, ref this._prevWidth);
            }
        }

        private void MacrosesEditorForm_ResizeEnd(object sender, EventArgs e)
        {
            this.ResizeGrid(this.gvMacros, ref this._prevWidth);
        }

        private void MacrosesEditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show(CANCEL_WARNING, CANCEL_WRN_CAPTION, MessageBoxButtons.YesNo);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = false;

            if (this._dataSession == null) return;

            try
            {
                if (this._dataSession.Transaction.IsActive)
                    this._dataSession.Transaction.Commit();
            }
            catch (Exception)
            {
                if (this._dataSession.Transaction.IsActive)
                    this._dataSession.Transaction.Rollback();
            }
            finally
            {
                this._dataSession.Close();
                this._dataSession.Dispose();
            }

            this._hwModule.Dispose();
        }

        #endregion

        #region Macros-related Event Handlers

        void _hwModule_DataReceivedFromPort(object sender, ComPortDataEventArgs e)
        {
           // throw new NotImplementedException();
        }

        void addSignalForm_SignalAdded(object sender, CustomEventArgs.ResultEntityEventArgs e)
        {
            this.BindSignals();
        }

        #endregion        

        #region Comboboxes Event Handlers

        private void cbSelectSignal_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.cbSelectSignal.SelectedItem is Signal)
                this._macrosesContainer.AssociatedSignal = this.cbSelectSignal.SelectedItem as Signal;

            this.btnSaveMacros.Enabled = (this.cbSelectSignal.SelectedIndex > 0) && (this.gvMacros.RowCount > 0);

            if (this.MacrosChanged != null)
                this.MacrosChanged(this, new EventArgs());
        }

        #endregion

        #region DataGridView Event Handlers

        private void gvMacros_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            this._rowIndex = e.RowIndex;
        }

        #endregion

        #region Utilities

        private void InitMacrosesContainer()
        {
            if (!String.IsNullOrEmpty(this.MacrosFileName))
            {
                try
                {
                    this._macrosesContainer = MacrosesContainer.GetFromFile(this.MacrosFileName);
                    Signal signal = this.cbSelectSignal.Items.OfType<Signal>().Where(x => x.Id == this._macrosesContainer.AssociatedSignal.Id).SingleOrDefault();
                    Int32 index = this.cbSelectSignal.Items.IndexOf(signal);
                    this.cbSelectSignal.SelectedIndex = index;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, ERROR);
                    this.Close();
                }
            }
            else
            {
                this._macrosesContainer = new MacrosesContainer();
            }
        }

        private void BindSignals()
        {
            this.cbSelectSignal.DisplayMember = "Name";
            this.cbSelectSignal.ValueMember = "Id";

            this.cbSelectSignal.Items.Clear();

            this.cbSelectSignal.Items.Add(new { Name = SELECT_TEXT, Id = -1 });
            this.cbSelectSignal.Items.AddRange(this._signalsRepo.GetAll().ToArray());
        }

        public void BindMacroses(ResultEntityEventArgs args)
        {
            MacrosCommand baseCommand = this._macrosCommandRepo.Get(x => x.Alias == args.CommandType.ToString()).SingleOrDefault();
            MacrosCommandWithParams command = new MacrosCommandWithParams(baseCommand);
            command.Params = args.Params;

            this._macrosesContainer.Commands.Add(command);
            this.BindMacrosesGrid();
        }

        public void SaveMacros(String fileName)
        {
            String result = String.Empty;

            try
            {
                this._macrosesContainer.SaveToFile(fileName);
                result = String.Format(MACROS_EXPORTED_SUCCESFULLY, fileName);
            }
            catch (Exception e)
            {
                result = e.Message;
            }

            MessageBox.Show(result);
        }

        private void BindMacrosesGrid()
        {
            this.gvMacros.DataSource = null;

            //Set AutoGenerateColumns False
            this.gvMacros.AutoGenerateColumns = false;

            //Set Columns Count
            this.gvMacros.ColumnCount = 2;

            //Add Columns
            this.gvMacros.Columns[0].Name = "CommandName";
            this.gvMacros.Columns[0].HeaderText = DG_COMMAND;
            this.gvMacros.Columns[0].DataPropertyName = "CommandName";
            this.gvMacros.Columns[0].Width = 150;

            this.gvMacros.Columns[1].Name = "Params";
            this.gvMacros.Columns[1].HeaderText = DG_PARAMS;
            this.gvMacros.Columns[1].DataPropertyName = "Params";
            this.gvMacros.Columns[1].Width = 300;

            var dataSource = this._macrosesContainer.Commands.Select(x => new { CommandName = x.Alias, Params = this.GetParamsString(x) }).ToList();
            this.gvMacros.DataSource = dataSource;

            this.btnSaveMacros.Enabled = (this.cbSelectSignal.SelectedIndex > 0) && (this.gvMacros.RowCount > 0);

            if (this.MacrosChanged != null)
                this.MacrosChanged(this, new EventArgs());
        }

        private void ResizeGrid(DataGridView dataGrid, ref int prevWidth)
        {
            if (prevWidth == 0)
                prevWidth = dataGrid.Width;
            if (prevWidth == dataGrid.Width)
                return;

            int fixedWidth = SystemInformation.VerticalScrollBarWidth +
            dataGrid.RowHeadersWidth + 2;
            int mul = 100 * (dataGrid.Width - fixedWidth) /
            (prevWidth - fixedWidth);
            int columnWidth;
            int total = 0;
            DataGridViewColumn lastVisibleCol = null;

            for (int i = 0; i < dataGrid.ColumnCount; i++)
                if (dataGrid.Columns[i].Visible)
                {
                    columnWidth = (dataGrid.Columns[i].Width * mul + 50) / 100;
                    dataGrid.Columns[i].Width =
                    Math.Max(columnWidth, dataGrid.Columns[i].MinimumWidth);
                    total += dataGrid.Columns[i].Width;
                    lastVisibleCol = dataGrid.Columns[i];
                }
            if (lastVisibleCol == null)
                return;
            columnWidth = dataGrid.Width - total +
            lastVisibleCol.Width - fixedWidth;
            lastVisibleCol.Width =
            Math.Max(columnWidth, lastVisibleCol.MinimumWidth);
            prevWidth = dataGrid.Width;
        }

        private String GetParamsString(MacrosCommandWithParams command)
        {
            MacrosCommandType commandType = (MacrosCommandType)Enum.Parse(typeof(MacrosCommandType), command.Alias);

            switch (commandType)
            {
                case MacrosCommandType.SEND:
                    Signal signal = this._signalsRepo.Get(x => x.Id.ToString().Equals(command.Params[0])).SingleOrDefault();
                    return String.Format(SEND_PARAMS_TEMPLATE, signal.Name, command.Params[1]);
                default:
                    return String.Join(";", command.Params.ToArray());
            }
        }

        #endregion       
                       
    }
}