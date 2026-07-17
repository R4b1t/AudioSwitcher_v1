using System;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using FortyOne.AudioSwitcher.HotKeyData;

namespace FortyOne.AudioSwitcher
{
    public enum HotKeyFormMode
    {
        Normal,
        Edit
    }

    public partial class HotKeyForm : Form
    {
        private readonly HotKey _hotkey;
        private readonly HotKey _linkedHotKey;
        private readonly HotKeyFormMode _mode = HotKeyFormMode.Normal;
        private DeviceState _deviceStateFilter = DeviceState.Active;

        private bool _firstFocus = true;

        public HotKeyForm()
        {
            InitializeComponent();

            _hotkey = new HotKey();

            // Keep in mind how the user wants the devices shown
            if (Program.Settings.ShowDisabledDevices)
                _deviceStateFilter |= DeviceState.Disabled;

            if (Program.Settings.ShowDisconnectedDevices)
                _deviceStateFilter |= DeviceState.Unplugged;

            cmbDevices.Items.Clear();
            foreach (var ad in AudioDeviceManager.Controller.GetPlaybackDevices(_deviceStateFilter))
                cmbDevices.Items.Add(ad);

            foreach (var ad in AudioDeviceManager.Controller.GetCaptureDevices(_deviceStateFilter))
                cmbDevices.Items.Add(ad);

            cmbDevices.DisplayMember = "FullName";
            cmbDevices.ValueMember = "ID";
        }

        public HotKeyForm(HotKey hk)
            : this()
        {
            _linkedHotKey = hk;

            _hotkey.DeviceId = hk.DeviceId;
            _hotkey.Key = hk.Key;
            _hotkey.Modifiers = hk.Modifiers;

            txtHotKey.Text = hk.HotKeyString;
            _firstFocus = false;

            _mode = HotKeyFormMode.Edit;

            Text = "Edit Hot Key";
            btnAdd.Text = "Save";
        }

        private void HotKeyForm_Load(object sender, EventArgs e)
        {
            AudioSwitcher.Instance.DisableHotKeyFunction = true;

            foreach (var o in cmbDevices.Items)
            {
                if (((IDevice)o).Id == _hotkey.DeviceId)
                {
                    cmbDevices.SelectedIndex = cmbDevices.Items.IndexOf(o);
                    break;
                }
            }

            cmbDevices.DisplayMember = "FullName";
            cmbDevices.ValueMember = "ID";
        }

        private void txtHotKey_Enter(object sender, EventArgs e)
        {
            if (_firstFocus)
            {
                txtHotKey.Text = "";
                _firstFocus = false;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (cmbDevices.SelectedItem == null)
            {
                errorProvider1.SetError(cmbDevices, "Please select a device");
                return;
            }

            if (_hotkey.Key == Keys.None)
            {
                errorProvider1.SetError(txtHotKey, "Please press a hotkey combination");
                return;
            }

            bool success;
            if (_mode == HotKeyFormMode.Edit)
            {
                // Update in place so a failed registration cannot delete the existing binding
                if (HotKeyManager.DuplicateHotKey(_hotkey, _linkedHotKey))
                {
                    errorProvider1.SetError(txtHotKey, "Hot key is already in use by another binding");
                    return;
                }

                success = HotKeyManager.UpdateHotKey(_linkedHotKey, _hotkey);
            }
            else
            {
                if (HotKeyManager.DuplicateHotKey(_hotkey))
                {
                    // Ghost entries are replaced inside AddHotKey; only report hard duplicates
                    var existing = HotKeyManager.FindDuplicate(_hotkey);
                    if (existing != null && existing.Device != null)
                    {
                        errorProvider1.SetError(txtHotKey, "Hot key is already in use by another binding");
                        return;
                    }
                }

                success = HotKeyManager.AddHotKey(_hotkey);
            }

            if (success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                errorProvider1.SetError(txtHotKey, "Hot key could not be registered (in use by another application or binding)");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }


        private void txtHotKey_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu)
                return;

            _hotkey.Key = e.KeyCode;
            _hotkey.Modifiers = Modifiers.None;

            if (e.Control)
                _hotkey.Modifiers = _hotkey.Modifiers | Modifiers.Control;

            if (e.Alt)
                _hotkey.Modifiers = _hotkey.Modifiers | Modifiers.Alt;

            if (e.Shift)
                _hotkey.Modifiers = _hotkey.Modifiers | Modifiers.Shift;

            if (e.Modifiers == Keys.LWin || e.Modifiers == Keys.RWin)
                _hotkey.Modifiers = _hotkey.Modifiers | Modifiers.Win;

            txtHotKey.Text = _hotkey.HotKeyString;

            errorProvider1.SetError(txtHotKey, "");

            if (_mode == HotKeyFormMode.Edit)
            {
                if (HotKeyManager.DuplicateHotKey(_hotkey, _linkedHotKey))
                    errorProvider1.SetError(txtHotKey, "Duplicate Hot Key Detected");
            }
            else if (HotKeyManager.DuplicateHotKey(_hotkey))
            {
                var existing = HotKeyManager.FindDuplicate(_hotkey);
                if (existing != null && existing.Device != null)
                    errorProvider1.SetError(txtHotKey, "Duplicate Hot Key Detected");
            }
        }

        private void cmbDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbDevices.SelectedItem == null)
                return;

            _hotkey.DeviceId = ((IDevice)cmbDevices.SelectedItem).Id;
        }

        private void HotKeyForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            AudioSwitcher.Instance.DisableHotKeyFunction = false;
        }

    }
}
