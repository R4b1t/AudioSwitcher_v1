using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.Observables;
using FortyOne.AudioSwitcher.Configuration;
using FortyOne.AudioSwitcher.Helpers;
using FortyOne.AudioSwitcher.HotKeyData;
using FortyOne.AudioSwitcher.Properties;
using Microsoft.Win32;

namespace FortyOne.AudioSwitcher
{
    public partial class AudioSwitcher : Form
    {
        /// <summary>
        ///     EASTER EGG! SHHH!
        /// </summary>
        private readonly Keys[] KONAMI_CODE = { Keys.Up, Keys.Up, Keys.Down, Keys.Down, Keys.Left, Keys.Right, Keys.Left, Keys.Right, Keys.B, Keys.A };

        private static AudioSwitcher _instance;
        private readonly Icon _originalTrayIcon;

        private readonly Dictionary<DeviceIcon, string> ICON_MAP = new Dictionary<DeviceIcon, string>
        {
            {DeviceIcon.Speakers, "3010"},
            {DeviceIcon.Headphones, "3011"},
            {DeviceIcon.LineIn, "3012"},
            {DeviceIcon.Digital, "3013"},
            {DeviceIcon.DesktopMicrophone, "3014"},
            {DeviceIcon.Headset, "3015"},
            {DeviceIcon.Phone, "3016"},
            {DeviceIcon.Monitor, "3017"},
            {DeviceIcon.StereoMix, "3018"},
            {DeviceIcon.Kinect, "3020"},
            {DeviceIcon.Unknown, "3020"}
        };

        private readonly string[] YOUTUBE_VIDEOS =
        {
            "http://www.youtube.com/watch?v=QJO3ROT-A4E",
            "http://www.youtube.com/watch?v=fWNaR-rxAic",
            "http://www.youtube.com/watch?v=X2WH8mHJnhM",
            "http://www.youtube.com/watch?v=dQw4w9WgXcQ",
            "http://www.youtube.com/watch?v=2Z4m4lnjxkY"
        };

        private DeviceState _deviceStateFilter = DeviceState.Active;
        private bool _doubleClickHappened;
        private bool _firstStart = true;
        private int _konamiIndex;
        private readonly DebounceDispatcher _deviceRefreshDebounce = new DebounceDispatcher(150);
        private readonly DebounceDispatcher _windowSizeDebounce = new DebounceDispatcher(400);
        private CheckBox _chkShowSwitchNotifications;
        private bool _trayNeedsRebuild = true;
        private Guid _lastNotifiedPlaybackId = Guid.Empty;
        public bool DisableHotKeyFunction;

        public AudioSwitcher()
        {
            InitializeComponent();
            HandleCreated += AudioSwitcher_HandleCreated;

            try
            {
                SetWindowTheme(listBoxPlayback.Handle, "Explorer", null);
                SetWindowTheme(listBoxRecording.Handle, "Explorer", null);
            }
            catch (Exception ex)
            {
                AppLog.Warn("SetWindowTheme failed: " + ex.Message);
            }

            lblVersion.Text = "Version: " + AssemblyVersion;
            lblCopyright.Text = AssemblyCopyright;

            _originalTrayIcon = new Icon(notifyIcon1.Icon, 32, 32);

            DisableUpdateUi();
            EnsureSwitchNotificationCheckbox();
            LoadSettings();

            AudioDeviceManager.Controller.AudioDeviceChanged.Subscribe(AudioDeviceManager_AudioDeviceChanged);

            HotKeyManager.HotKeyPressed += HotKeyManager_HotKeyPressed;
            hotKeyBindingSource.DataSource = HotKeyManager.HotKeys;

            // After sleep/hibernate Windows may drop RegisterHotKey bindings
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            ProfileManager.EnsureLoaded();
        }

        public static AudioSwitcher Instance
        {
            get { return _instance ?? (_instance = new AudioSwitcher()); }
        }

        private IDevice SelectedPlaybackDevice
        {
            get
            {
                if (listBoxPlayback.SelectedItems.Count > 0)
                    return ((IDevice)listBoxPlayback.SelectedItems[0].Tag);
                return null;
            }
        }

        public IDevice SelectedRecordingDevice
        {
            get
            {
                if (listBoxRecording.SelectedItems.Count > 0)
                    return ((IDevice)listBoxRecording.SelectedItems[0].Tag);
                return null;
            }
        }

        public bool TrayIconVisible
        {
            get { return notifyIcon1.Visible; }
            set
            {
                try
                {
                    notifyIcon1.Visible = value;
                }
                catch
                {
                } // rubbish error
            }
        }

        public string AssemblyTitle
        {
            get
            {
                var attributes =
                    Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    var titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get { return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion; }
        }

        public string AssemblyDescription
        {
            get
            {
                var attributes =
                    Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                var attributes =
                    Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                var attributes =
                    Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                var attributes =
                    Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }

        private void AudioSwitcher_HandleCreated(object sender, EventArgs e)
        {
            BeginInvoke(new Action(Form_Load));
        }

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        private async void Form_Load()
        {
            var icon = Icon.ExtractAssociatedIcon(Environment.ExpandEnvironmentVariables("%windir%\\system32\\control.exe"));

            if (icon != null)
            {
                using (var i = new Bitmap(25, 25))
                using (var g = Graphics.FromImage(i))
                {
                    g.DrawImage(new Bitmap(icon.ToBitmap(), 25, 25), new Rectangle(0, 0, 25, 25));
                    g.FillRectangle(Brushes.Black, new Rectangle(0, 13, 12, 12));
                    g.DrawImage(Resources.shortcut, new Rectangle(1, 14, 10, 10));
                    openControlPanelPlayback.Image = i.Clone() as Image;
                    openControlPanelRecording.Image = i.Clone() as Image;
                }

                icon.Dispose();
            }

            var dev = AudioDeviceManager.Controller.GetDevice(Program.Settings.StartupPlaybackDeviceID, DeviceState.All);

            if (dev != null)
            {
                await dev.SetAsDefaultAsync();
                if (Program.Settings.DualSwitchMode)
                    await dev.SetAsDefaultCommunicationsAsync();
            }

            dev = AudioDeviceManager.Controller.GetDevice(Program.Settings.StartupRecordingDeviceID, DeviceState.All);

            if (dev != null)
            {
                await dev.SetAsDefaultAsync();
                if (Program.Settings.DualSwitchMode)
                    await dev.SetAsDefaultCommunicationsAsync();
            }

            BeginInvoke((Action)(() =>
            {
                RefreshPlaybackDevices();
                RefreshRecordingDevices();
            }));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
#if DEBUG
            btnTestError.Visible = true;
#endif
            // Disable cargo-cult working-set trimmer
            memoryCleaner.Enabled = false;
            memoryCleaner.Stop();
        }

        private void DisableUpdateUi()
        {
            // Updates are permanently disabled in this fork
            try
            {
                btnCheckUpdate.Visible = false;
                btnCheckUpdate.Enabled = false;
                chkNotifyUpdates.Visible = false;
                chkNotifyUpdates.Enabled = false;
                statusLabelUpdate.Visible = false;
                updateAvailableToolStripMenuItem.Visible = false;
            }
            catch
            {
            }
        }

        private void EnsureSwitchNotificationCheckbox()
        {
            if (_chkShowSwitchNotifications != null)
                return;

            _chkShowSwitchNotifications = new CheckBox
            {
                AutoSize = true,
                Location = new Point(14, 200),
                Name = "chkShowSwitchNotifications",
                Size = new Size(220, 17),
                TabIndex = 20,
                Text = "Show notification when switching device",
                UseVisualStyleBackColor = true
            };
            _chkShowSwitchNotifications.CheckedChanged += (s, e) =>
            {
                Program.Settings.ShowSwitchNotifications = _chkShowSwitchNotifications.Checked;
            };
            tapSettings.Controls.Add(_chkShowSwitchNotifications);
        }

        private void AudioDeviceManager_AudioDeviceChanged(DeviceChangedArgs e)
        {
            // Coalesce bursty CoreAudio events into one UI refresh
            var changedType = e.ChangedType;
            var isPlayback = e.Device != null && e.Device.IsPlaybackDevice;
            var isCapture = e.Device != null && e.Device.IsCaptureDevice;

            _deviceRefreshDebounce.Debounce(() =>
            {
                Action refreshAction = () =>
                {
                    if (isPlayback)
                        RefreshPlaybackDevices(false);
                    if (isCapture)
                        RefreshRecordingDevices(false);

                    if (changedType == DeviceChangedType.DeviceAdded
                        || changedType == DeviceChangedType.DeviceRemoved
                        || changedType == DeviceChangedType.StateChanged
                        || changedType == DeviceChangedType.DefaultChanged)
                    {
                        HotKeyManager.RefreshHotkeys();
                        RefreshGrid();
                        _trayNeedsRebuild = true;
                        RefreshNotifyIconItems();
                    }
                };

                if (InvokeRequired)
                    BeginInvoke(refreshAction);
                else
                    refreshAction();
            });
        }

        private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume)
                return;

            Action reregister = () =>
            {
                HotKeyManager.ReregisterHotkeys();
                RefreshGrid();
            };

            if (InvokeRequired)
                BeginInvoke(reregister);
            else
                reregister();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (Program.Settings.StartMinimized && _firstStart && !IsDisposed)
            {
                value = false;
                _firstStart = false;
                if (!IsHandleCreated) CreateHandle();
            }

            base.SetVisibleCore(value);
        }

        private static void Donate()
        {
            Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=Q9TDQPY4B369A");
        }

        private void mnuFavouritePlaybackDevice_Click(object sender, EventArgs e)
        {
            if (SelectedPlaybackDevice == null)
                return;

            var id = SelectedPlaybackDevice.Id;
            //if checked then we need to remove

            if (mnuFavouritePlaybackDevice.Checked)
                FavouriteDeviceManager.RemoveFavouriteDevice(SelectedPlaybackDevice.Id);
            else
                FavouriteDeviceManager.AddFavouriteDevice(SelectedPlaybackDevice.Id);

            PostPlaybackMenuClick(id);
        }

        private void mnuFavouriteRecordingDevice_Click(object sender, EventArgs e)
        {
            if (SelectedRecordingDevice == null)
                return;

            var id = SelectedRecordingDevice.Id;

            if (mnuFavouriteRecordingDevice.Checked)
                FavouriteDeviceManager.RemoveFavouriteDevice(SelectedRecordingDevice.Id);
            else
                FavouriteDeviceManager.AddFavouriteDevice(SelectedRecordingDevice.Id);

            PostRecordingMenuClick(id);
        }

        private void chkDisableHotKeys_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.DisableHotKeys = chkDisableHotKeys.Checked;
            if (Program.Settings.DisableHotKeys)
                HotKeyManager.UnregisterAllHotkeys();
            else
                HotKeyManager.ReregisterHotkeys();
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            _doubleClickHappened = false;

            if (e.Button == MouseButtons.Left)
            {
                var t = new Timer();
                t.Tick += t_Tick;
                t.Interval = SystemInformation.DoubleClickTime;
                t.Start();
            }
        }

        private async void t_Tick(object sender, EventArgs e)
        {
            ((Timer)sender).Stop();
            if (_doubleClickHappened)
                return;

            if (Program.Settings.EnableQuickSwitch)
            {
                if (FavouriteDeviceManager.FavouriteDeviceCount > 0)
                {
                    var changed = false;
                    var currentDefaultDevice = AudioDeviceManager.Controller.DefaultPlaybackDevice;
                    var candidate = FavouriteDeviceManager.GetNextFavouritePlaybackDevice(currentDefaultDevice);
                    var attemptsCount = FavouriteDeviceManager.FavouritePlaybackDeviceCount;
                    for (var i = 0; !changed && i < attemptsCount; i++)
                    {
                        changed = await candidate.SetAsDefaultAsync();

                        if (changed && Program.Settings.DualSwitchMode)
                            await candidate.SetAsDefaultCommunicationsAsync();

                        if (!changed)
                            candidate = FavouriteDeviceManager.GetNextFavouritePlaybackDevice(candidate);
                    }
                }
                else
                {
                    var currentDefault = AudioDeviceManager.Controller.DefaultPlaybackDevice;
                    var playbackDevices = (await AudioDeviceManager.Controller.GetPlaybackDevicesAsync(DeviceState.Active))
                                            .OrderBy(x => x.Name)
                                            .ToList();

                    var deviceIndex = playbackDevices.IndexOf(currentDefault);
                    var newIndex = deviceIndex;

                    while (true)
                    {
                        newIndex = (newIndex + 1) % playbackDevices.Count;

                        if (newIndex == deviceIndex)
                            break;

                        try
                        {
                            var newDevice = playbackDevices[newIndex];
                            if (await newDevice.SetAsDefaultAsync())
                                break;
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            else
            {
                RefreshNotifyIconItems();
                var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                mi.Invoke(notifyIcon1, null);
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            _doubleClickHappened = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            throw new Exception("Fail Message");
        }

        private void AudioSwitcher_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == KONAMI_CODE[_konamiIndex])
            {
                if (_konamiIndex == KONAMI_CODE.Length - 1)
                {
                    _konamiIndex = 0;

                    var rand = new Random();
                    var index = rand.Next(YOUTUBE_VIDEOS.Length);
                    Process.Start(YOUTUBE_VIDEOS[index]);
                }
                else
                {
                    ++_konamiIndex;
                }
            }
            else
            {
                _konamiIndex = 0;
            }
        }

        private void listBoxPlayback_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            RefreshPlaybackDropDownButton();
        }

        private void listBoxRecording_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            RefreshRecordingDropDownButton();
        }

        private void btnCheckUpdate_Click(object sender, EventArgs e)
        {
            // Updates disabled
            MessageBox.Show(this, "Automatic updates are disabled in this build.", "Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void setHotKeyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (SelectedPlaybackDevice == null)
                return;

            var hotkey = HotKeyManager.HotKeys.FirstOrDefault(x => x.DeviceId == SelectedPlaybackDevice.Id);

            if (hotkey == null)
            {
                hotkey = new HotKey();
                hotkey.DeviceId = SelectedPlaybackDevice.Id;
            }

            var hkf = new HotKeyForm(hotkey);
            hkf.ShowDialog(this);
        }

        private void setHotKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedRecordingDevice == null)
                return;

            var hotkey = HotKeyManager.HotKeys.FirstOrDefault(x => x.DeviceId == SelectedRecordingDevice.Id);

            if (hotkey == null)
            {
                hotkey = new HotKey();
                hotkey.DeviceId = SelectedRecordingDevice.Id;
            }

            var hkf = new HotKeyForm(hotkey);
            hkf.ShowDialog(this);
        }

        private void memoryCleaner_Tick(object sender, EventArgs e)
        {
            // Intentionally empty — EmptyWorkingSet thrashing was removed
        }

        private void linkLabel1_Click(object sender, EventArgs e)
        {
            Process.Start("http://services.audioswit.ch/versions/");
        }

        private void mnuSetPlaybackStartupDevice_Click(object sender, EventArgs e)
        {
            if (SelectedPlaybackDevice == null)
                return;

            if (Program.Settings.StartupPlaybackDeviceID == SelectedPlaybackDevice.Id)
                Program.Settings.StartupPlaybackDeviceID = Guid.Empty;
            else
                Program.Settings.StartupPlaybackDeviceID = SelectedPlaybackDevice.Id;

            RefreshPlaybackDropDownButton();
        }

        private void mnuSetRecordingStartupDevice_Click(object sender, EventArgs e)
        {
            if (SelectedRecordingDevice == null)
                return;

            if (Program.Settings.StartupRecordingDeviceID == SelectedRecordingDevice.Id)
                Program.Settings.StartupRecordingDeviceID = Guid.Empty;
            else
                Program.Settings.StartupRecordingDeviceID = SelectedRecordingDevice.Id;

            RefreshRecordingDropDownButton();
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://audioswit.ch/er?utm_source=client&utm_medium=direct&utm_campaign=client_" + AssemblyVersion.Replace(".", "_"));
        }

        private void chkShowDiabledDevices_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.ShowDisabledDevices = chkShowDiabledDevices.Checked;

            //Set, or remove the disconnected filter
            if (Program.Settings.ShowDisabledDevices)
                _deviceStateFilter |= DeviceState.Disabled;
            else
                _deviceStateFilter ^= DeviceState.Disabled;

            if (IsHandleCreated)
            {
                BeginInvoke((Action)(() =>
                {
                    RefreshPlaybackDevices();
                    RefreshRecordingDevices();
                }));
            }
        }

        private void chkShowDisconnectedDevices_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.ShowDisconnectedDevices = chkShowDisconnectedDevices.Checked;

            //Set, or remove the disconnected filter
            if (Program.Settings.ShowDisconnectedDevices)
                _deviceStateFilter |= DeviceState.Unplugged;
            else
                _deviceStateFilter ^= DeviceState.Unplugged;

            if (IsHandleCreated)
            {
                BeginInvoke((Action)(() =>
                {
                    RefreshPlaybackDevices();
                    RefreshRecordingDevices();
                }));
            }
        }

        private void playbackStrip_Opening(object sender, CancelEventArgs e)
        {
            if (SelectedPlaybackDevice == null)
                e.Cancel = true;
        }

        private void recordingStrip_Opening(object sender, CancelEventArgs e)
        {
            if (SelectedRecordingDevice == null)
                e.Cancel = true;
        }

        private void linkIssues_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/xenolightning/AudioSwitcher_v1/issues");
        }

        private void linkWiki_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/xenolightning/AudioSwitcher_v1/wiki");
        }

        private void chkShowDPDeviceIconInTray_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.ShowDPDeviceIconInTray = chkShowDPDeviceIconInTray.Checked;
            RefreshTrayIcon();
        }

        private void preferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
        }

        private void btnAddHotKey_Click(object sender, EventArgs e)
        {
            var hkf = new HotKeyForm();
            hkf.ShowDialog(this);
            RefreshGrid();
        }

        private void btnEditHotKey_Click(object sender, EventArgs e)
        {
            if (hotKeyBindingSource.Current != null)
            {
                var hkf = new HotKeyForm((HotKey)hotKeyBindingSource.Current);
                hkf.ShowDialog(this);
                RefreshGrid();
            }
        }

        private void btnDeleteHotKey_Click(object sender, EventArgs e)
        {
            if (hotKeyBindingSource.Current != null)
            {
                DialogResult result = MessageBox.Show(this, "Do you want to delete this hotkey?", "", MessageBoxButtons.OKCancel);

                if (result == DialogResult.OK)
                {
                    HotKeyManager.DeleteHotKey((HotKey)hotKeyBindingSource.Current);
                    RefreshGrid();
                }
            }
        }

        private void btnClearAllHotKeys_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(this, "Do you want to clear all hotkeys?", "", MessageBoxButtons.OKCancel);

            if (result == DialogResult.OK)
            {
                HotKeyManager.ClearAll();
                RefreshGrid();
            }
        }

        private void RefreshGrid()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RefreshGrid));
                return;
            }

            hotKeyBindingSource.ResetBindings(false);
            dataGridView1.Refresh();
        }

        private void LoadSettings()
        {
            // Re-apply so registry run key stays in sync with the setting
            Program.Settings.AutoStartWithWindows = Program.Settings.AutoStartWithWindows;

            chkCloseToTray.Checked = Program.Settings.CloseToTray;
            chkStartMinimized.Checked = Program.Settings.StartMinimized;
            chkAutoStartWithWindows.Checked = Program.Settings.AutoStartWithWindows;
            chkDisableHotKeys.Checked = Program.Settings.DisableHotKeys;
            chkQuickSwitch.Checked = Program.Settings.EnableQuickSwitch;
            chkDualSwitchMode.Checked = Program.Settings.DualSwitchMode;
            chkNotifyUpdates.Checked = false;

            chkShowDiabledDevices.Checked = Program.Settings.ShowDisabledDevices;
            chkShowUnknownDevicesInHotkeyList.Checked = Program.Settings.ShowUnknownDevicesInHotkeyList;
            chkShowDisconnectedDevices.Checked = Program.Settings.ShowDisconnectedDevices;
            chkShowDPDeviceIconInTray.Checked = Program.Settings.ShowDPDeviceIconInTray;

            if (_chkShowSwitchNotifications != null)
                _chkShowSwitchNotifications.Checked = Program.Settings.ShowSwitchNotifications;

            Width = Program.Settings.WindowWidth;
            Height = Program.Settings.WindowHeight;

            FavouriteDeviceManager.FavouriteDevicesChanged += AudioDeviceManger_FavouriteDevicesChanged;
            FavouriteDeviceManager.LoadFavouriteDevices(Program.Settings.GetFavouriteDeviceIds());

            if (Program.Settings.ShowDisabledDevices)
                _deviceStateFilter |= DeviceState.Disabled;

            if (Program.Settings.ShowDisconnectedDevices)
                _deviceStateFilter |= DeviceState.Unplugged;
        }

        private void AudioDeviceManger_FavouriteDevicesChanged(List<Guid> ids)
        {
            Program.Settings.SetFavouriteDeviceIds(ids);
            _trayNeedsRebuild = true;
            RefreshNotifyIconItems();
        }

        private void RefreshPlaybackDevices()
        {
            RefreshPlaybackDevices(true);
        }

        private void RefreshPlaybackDevices(bool refreshTray)
        {
            DeviceListViewHelper.Populate(
                listBoxPlayback,
                AudioDeviceManager.Controller.GetPlaybackDevices(_deviceStateFilter).ToList(),
                imageList1,
                ICON_MAP,
                ExtractIconFromPath);

            if (refreshTray)
            {
                _trayNeedsRebuild = true;
                RefreshNotifyIconItems();
            }
        }

        private static Icon ExtractIconFromPath(string path)
        {
            try
            {
                var iconPath = path.Split(',');
                Icon icon;
                if (iconPath.Length == 2)
                    icon = IconExtractor.Extract(Environment.ExpandEnvironmentVariables(iconPath[0]),
                        Int32.Parse(iconPath[1]), true);
                else
                    icon = new Icon(iconPath[0]);

                return icon;
            }
            catch
            {
                //return a digital as a place holder
                return IconExtractor.Extract(Environment.ExpandEnvironmentVariables("%windir%\\system32\\mmres.dll"), -3013, true);
            }
        }

        private void RefreshRecordingDevices()
        {
            RefreshRecordingDevices(true);
        }

        private void RefreshRecordingDevices(bool refreshTray)
        {
            DeviceListViewHelper.Populate(
                listBoxRecording,
                AudioDeviceManager.Controller.GetCaptureDevices(_deviceStateFilter).ToList(),
                imageList1,
                ICON_MAP,
                ExtractIconFromPath);

            if (refreshTray)
            {
                _trayNeedsRebuild = true;
                RefreshNotifyIconItems();
            }
        }

        private void RefreshNotifyIconItems()
        {
            if (!_trayNeedsRebuild)
            {
                RefreshTrayIcon();
                UpdateTrayTooltip();
                return;
            }

            notifyIconStrip.Items.Clear();

            var playbackCount = 0;
            var recordingCount = 0;

            foreach (var ad in AudioDeviceManager.Controller.GetPlaybackDevices(_deviceStateFilter).ToList())
            {
                if (FavouriteDeviceManager.FavouriteDeviceCount > 0 && !FavouriteDeviceManager.IsFavouriteDevice(ad))
                    continue;

                notifyIconStrip.Items.Add(new ToolStripMenuItem
                {
                    Text = ad.FullName,
                    Tag = ad,
                    Checked = ad.IsDefaultDevice
                });
                playbackCount++;
            }

            if (playbackCount > 0)
                notifyIconStrip.Items.Add(new ToolStripSeparator());

            foreach (var ad in AudioDeviceManager.Controller.GetCaptureDevices(_deviceStateFilter).ToList())
            {
                if (FavouriteDeviceManager.FavouriteDeviceCount > 0 && !FavouriteDeviceManager.IsFavouriteDevice(ad))
                    continue;

                notifyIconStrip.Items.Add(new ToolStripMenuItem
                {
                    Text = ad.FullName,
                    Tag = ad,
                    Checked = ad.IsDefaultDevice
                });
                recordingCount++;
            }

            if (recordingCount > 0)
                notifyIconStrip.Items.Add(new ToolStripSeparator());

            // Profiles submenu
            var profiles = ProfileManager.Profiles;
            if (profiles.Count > 0)
            {
                var profilesMenu = new ToolStripMenuItem("Profiles");
                foreach (var profile in profiles)
                {
                    var p = profile;
                    var item = new ToolStripMenuItem(p.Name);
                    item.Click += async (s, e) =>
                    {
                        await ProfileManager.ApplyProfileAsync(p);
                        ShowSwitchNotification("Profile: " + p.Name);
                        _trayNeedsRebuild = true;
                        RefreshPlaybackDevices();
                        RefreshRecordingDevices();
                    };
                    profilesMenu.DropDownItems.Add(item);
                }
                profilesMenu.DropDownItems.Add(new ToolStripSeparator());
                var saveItem = new ToolStripMenuItem("Save current as profile...");
                saveItem.Click += SaveCurrentProfileFromTray;
                profilesMenu.DropDownItems.Add(saveItem);
                notifyIconStrip.Items.Add(profilesMenu);
                notifyIconStrip.Items.Add(new ToolStripSeparator());
            }
            else
            {
                var saveItem = new ToolStripMenuItem("Save current as profile...");
                saveItem.Click += SaveCurrentProfileFromTray;
                notifyIconStrip.Items.Add(saveItem);
                notifyIconStrip.Items.Add(new ToolStripSeparator());
            }

            notifyIconStrip.Items.Add(preferencesToolStripMenuItem);
            notifyIconStrip.Items.Add(exitToolStripMenuItem);

            UpdateTrayTooltip();
            RefreshTrayIcon();
            _trayNeedsRebuild = false;
        }

        private void UpdateTrayTooltip()
        {
            var defaultDevice = AudioDeviceManager.Controller.DefaultPlaybackDevice;
            var notifyText = "Audio Switcher";

            if (defaultDevice != null)
            {
                var devName = defaultDevice.FullName ?? defaultDevice.Name ?? notifyText;
                notifyText = devName.Length >= 64 ? devName.Substring(0, 60) + "..." : devName;
            }

            try
            {
                notifyIcon1.Text = notifyText;
            }
            catch
            {
                // NotifyIcon.Text max length enforcement can throw on some systems
                notifyIcon1.Text = "Audio Switcher";
            }
        }

        private void SaveCurrentProfileFromTray(object sender, EventArgs e)
        {
            var name = PromptForProfileName("My Profile");
            if (string.IsNullOrWhiteSpace(name))
                return;

            var playback = AudioDeviceManager.Controller.DefaultPlaybackDevice;
            var recording = AudioDeviceManager.Controller.DefaultCaptureDevice;

            ProfileManager.SaveProfile(
                name.Trim(),
                playback != null ? playback.Id : Guid.Empty,
                recording != null ? recording.Id : Guid.Empty,
                Program.Settings.DualSwitchMode);

            _trayNeedsRebuild = true;
            RefreshNotifyIconItems();
            ShowSwitchNotification("Saved profile: " + name.Trim());
        }

        private string PromptForProfileName(string defaultName)
        {
            using (var form = new Form())
            {
                form.Text = "Save Audio Profile";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(320, 110);
                form.ShowInTaskbar = false;

                var label = new Label { Text = "Profile name:" };
                label.SetBounds(12, 12, 290, 20);

                var textBox = new TextBox { Text = defaultName };
                textBox.SetBounds(12, 36, 290, 23);

                var ok = new Button { Text = "Save", DialogResult = DialogResult.OK };
                ok.SetBounds(146, 70, 75, 26);

                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
                cancel.SetBounds(227, 70, 75, 26);

                form.Controls.AddRange(new Control[] { label, textBox, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog(this) == DialogResult.OK ? textBox.Text : null;
            }
        }

        private void ShowSwitchNotification(string message)
        {
            if (!Program.Settings.ShowSwitchNotifications)
                return;

            try
            {
                notifyIcon1.BalloonTipTitle = "Audio Switcher";
                notifyIcon1.BalloonTipText = message;
                notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
                notifyIcon1.ShowBalloonTip(1500);
            }
            catch (Exception ex)
            {
                AppLog.Warn("Balloon tip failed: " + ex.Message);
            }
        }

        private void RefreshTrayIcon()
        {
            var defaultDevice = AudioDeviceManager.Controller.DefaultPlaybackDevice;
            var oldIcon = notifyIcon1.Icon;

            if (defaultDevice != null && Program.Settings.ShowDPDeviceIconInTray)
                notifyIcon1.Icon = ExtractIconFromPath(defaultDevice.IconPath);
            else
                notifyIcon1.Icon = _originalTrayIcon;

            if (oldIcon.Handle != _originalTrayIcon.Handle)
                oldIcon.Dispose();
        }

        private void RefreshPlaybackDropDownButton()
        {
            if (SelectedPlaybackDevice == null)
            {
                btnSetPlaybackDefault.Enabled = false;
                return;
            }

            mnuSetPlaybackDefault.CheckState = SelectedPlaybackDevice.IsDefaultDevice
                ? CheckState.Checked
                : CheckState.Unchecked;

            mnuSetPlaybackCommunicationDefault.CheckState = SelectedPlaybackDevice.IsDefaultCommunicationsDevice
                ? CheckState.Checked
                : CheckState.Unchecked;

            mnuFavouritePlaybackDevice.CheckState = FavouriteDeviceManager.IsFavouriteDevice(SelectedPlaybackDevice.Id)
                ? CheckState.Checked
                : CheckState.Unchecked;

            mnuSetPlaybackStartupDevice.CheckState = Program.Settings.StartupPlaybackDeviceID ==
                                                     SelectedPlaybackDevice.Id
                ? CheckState.Checked
                : CheckState.Unchecked;

            if (SelectedPlaybackDevice.State == DeviceState.Unplugged)
            {
                btnSetPlaybackDefault.Enabled = false;
                mnuFavouritePlaybackDevice.Enabled = false;
            }
            else
            {
                btnSetPlaybackDefault.Enabled = true;
                mnuFavouritePlaybackDevice.Enabled = true;
            }
        }

        private void RefreshRecordingDropDownButton()
        {
            if (SelectedRecordingDevice == null)
            {
                btnSetRecordingDefault.Enabled = false;
                return;
            }

            mnuSetRecordingDefault.CheckState = SelectedRecordingDevice.IsDefaultDevice
                ? CheckState.Checked
                : CheckState.Unchecked;

            mnuSetRecordingCommunicationDefault.CheckState = SelectedRecordingDevice.IsDefaultCommunicationsDevice
                ? CheckState.Checked
                : CheckState.Unchecked;

            mnuFavouriteRecordingDevice.CheckState = FavouriteDeviceManager.IsFavouriteDevice(SelectedRecordingDevice.Id)
                ? CheckState.Checked
                : CheckState.Unchecked;

            mnuSetRecordingStartupDevice.CheckState = Program.Settings.StartupRecordingDeviceID ==
                                                      SelectedRecordingDevice.Id
                ? CheckState.Checked
                : CheckState.Unchecked;

            if (SelectedRecordingDevice.State == DeviceState.Unplugged)
            {
                btnSetRecordingDefault.Enabled = false;
                mnuFavouriteRecordingDevice.Enabled = false;
            }
            else
            {
                btnSetRecordingDefault.Enabled = true;
                mnuFavouriteRecordingDevice.Enabled = true;
            }
        }

        private async void mnuSetPlaybackCommunicationDefault_Click(object sender, EventArgs e)
        {
            if (SelectedPlaybackDevice == null)
                return;

            var id = SelectedPlaybackDevice.Id;
            await SelectedPlaybackDevice.SetAsDefaultCommunicationsAsync();
            PostPlaybackMenuClick(id);
        }

        private async void mnuSetPlaybackDefault_Click(object sender, EventArgs e)
        {
            if (SelectedPlaybackDevice == null)
                return;

            var id = SelectedPlaybackDevice.Id;
            await SelectedPlaybackDevice.SetAsDefaultAsync();
            PostPlaybackMenuClick(id);
        }

        private void PostPlaybackMenuClick(Guid id)
        {
            RefreshPlaybackDevices();
            RefreshPlaybackDropDownButton();
            for (var i = 0; i < listBoxPlayback.Items.Count; i++)
            {
                if (((IDevice)listBoxPlayback.Items[i].Tag).Id == id)
                {
                    listBoxPlayback.Items[i].Selected = true;
                    break;
                }
            }
        }

        private async void mnuSetRecordingDefault_Click(object sender, EventArgs e)
        {
            if (SelectedRecordingDevice == null)
                return;

            var id = SelectedRecordingDevice.Id;
            await SelectedRecordingDevice.SetAsDefaultAsync();
            PostRecordingMenuClick(id);
        }

        private void PostRecordingMenuClick(Guid id)
        {
            RefreshRecordingDevices();
            RefreshRecordingDropDownButton();
            for (var i = 0; i < listBoxRecording.Items.Count; i++)
            {
                if (((IDevice)listBoxRecording.Items[i].Tag).Id == id)
                {
                    listBoxRecording.Items[i].Selected = true;
                    break;
                }
            }
        }

        private async void mnuSetRecordingCommunicationDefault_Click(object sender, EventArgs e)
        {
            if (SelectedRecordingDevice == null)
                return;

            var id = SelectedRecordingDevice.Id;
            await SelectedRecordingDevice.SetAsDefaultCommunicationsAsync();
            PostRecordingMenuClick(id);
        }

        private async void HotKeyManager_HotKeyPressed(object sender, EventArgs e)
        {
            if (DisableHotKeyFunction || Program.Settings.DisableHotKeys)
                return;

            var hk = sender as HotKey;
            if (hk == null)
                return;

            var device = hk.Device;
            if (device == null || device.IsDefaultDevice)
                return;

            await device.SetAsDefaultAsync();

            if (Program.Settings.DualSwitchMode)
                await device.SetAsDefaultCommunicationsAsync();

            if (device.IsPlaybackDevice && device.Id != _lastNotifiedPlaybackId)
            {
                _lastNotifiedPlaybackId = device.Id;
                ShowSwitchNotification("Switched to " + (device.FullName ?? device.Name));
            }
            else if (device.IsCaptureDevice)
            {
                ShowSwitchNotification("Recording: " + (device.FullName ?? device.Name));
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (Program.Settings.CloseToTray)
                {
                    e.Cancel = true;
                    Hide();
                    return;
                }
            }

            // Real shutdown
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            HotKeyManager.UnregisterAllHotkeys();
            _deviceRefreshDebounce.Dispose();
            _windowSizeDebounce.Dispose();
            try
            {
                Program.Settings.Flush();
            }
            catch (Exception ex)
            {
                AppLog.Error("Flush on close failed", ex);
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Show();
            BringToFront();
            SetForegroundWindow(Handle);
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            //RefreshPlaybackDevices();
            //RefreshRecordingDevices();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private async void notifyIconStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem != null && e.ClickedItem.Tag is IDevice)
            {
                var dev = (IDevice)e.ClickedItem.Tag;
                await dev.SetAsDefaultAsync();

                if (Program.Settings.DualSwitchMode)
                    await dev.SetAsDefaultCommunicationsAsync();

                ShowSwitchNotification("Switched to " + (dev.FullName ?? dev.Name));
                _trayNeedsRebuild = true;
                RefreshNotifyIconItems();
            }
        }

        private void chkCloseToTray_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.CloseToTray = chkCloseToTray.Checked;
        }

        private void chkAutoStartWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.AutoStartWithWindows = chkAutoStartWithWindows.Checked;
        }

        private void chkStartMinimized_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.StartMinimized = chkStartMinimized.Checked;
        }

        private void chkQuickSwitch_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.EnableQuickSwitch = chkQuickSwitch.Checked;
        }

        private void chkDualSwitchMode_CheckedChanged(object sender, EventArgs e)
        {
            Program.Settings.DualSwitchMode = chkDualSwitchMode.Checked;
        }

        private void AudioSwitcher_ResizeEnd(object sender, EventArgs e)
        {
            var w = Width;
            var h = Height;
            _windowSizeDebounce.Debounce(() =>
            {
                Program.Settings.WindowWidth = w;
                Program.Settings.WindowHeight = h;
            });
        }

        private void statusLabelUpdate_Click(object sender, EventArgs e)
        {
            // Updates disabled
        }

        private void statusLabelDonate_Click(object sender, EventArgs e)
        {
            Donate();
        }

        private void updateAvailableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Updates disabled
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.twitter.com/xenolightning");
        }

        private void pictureBox1_Click_1(object sender, EventArgs e)
        {
            Process.Start("https://github.com/xenolightning/AudioSwitcher_v1");
        }

        private void chkNotifyUpdates_CheckedChanged(object sender, EventArgs e)
        {
            // Updates disabled — keep setting off
            Program.Settings.UpdateNotificationsEnabled = false;
        }

        private void twitterLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://www.twitter.com/xenolightning");
        }

        private void openControlPanelPlayback_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl,,0"));
            }
            catch
            {
                //Ignored, something went wrong when trying to open CPL
            }
        }

        private void openControlPanelRecording_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl,,1"));
            }
            catch
            {
                //Ignored, something went wrong when trying to open CPL
            }
        }

        private async void listBoxPlayback_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (SelectedPlaybackDevice == null)
                return;

            var id = SelectedPlaybackDevice.Id;
            await SelectedPlaybackDevice.SetAsDefaultAsync();
            PostPlaybackMenuClick(id);
        }

        private async void listBoxRecording_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (SelectedRecordingDevice == null)
                return;

            var id = SelectedRecordingDevice.Id;
            await SelectedRecordingDevice.SetAsDefaultAsync();
            PostRecordingMenuClick(id);
        }

		private void chkShowUnknownDevicesInHotkeyList_CheckedChanged(object sender, EventArgs e)
		{
			Program.Settings.ShowUnknownDevicesInHotkeyList = chkShowUnknownDevicesInHotkeyList.Checked;

			if (IsHandleCreated)
			{
				BeginInvoke((Action)(() =>
				{
					HotKeyManager.RefreshHotkeys();
					RefreshGrid();
				}));
			}
		}
	}
}