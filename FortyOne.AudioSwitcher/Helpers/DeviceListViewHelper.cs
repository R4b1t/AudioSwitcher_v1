using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using FortyOne.AudioSwitcher.Properties;

namespace FortyOne.AudioSwitcher.Helpers
{
    /// <summary>
    /// Shared device list rendering for playback/recording ListViews.
    /// </summary>
    public static class DeviceListViewHelper
    {
        public static void Populate(
            ListView listView,
            IEnumerable<IDevice> devices,
            ImageList imageList,
            IDictionary<DeviceIcon, string> iconMap,
            Func<string, Icon> extractIcon)
        {
            listView.BeginUpdate();
            try
            {
                listView.Items.Clear();

                foreach (var ad in devices)
                {
                    var li = new ListViewItem
                    {
                        Text = ad.Name,
                        Tag = ad
                    };
                    li.SubItems.Add(new ListViewItem.ListViewSubItem(li, ad.InterfaceName));

                    try
                    {
                        var imageKey = "";
                        var imageMod = "";

                        string mapped;
                        if (iconMap.TryGetValue(ad.Icon, out mapped))
                            imageKey = mapped;

                        if (ad.IsDefaultDevice)
                        {
                            li.SubItems.Add(new ListViewItem.ListViewSubItem(li, "Default Device"));
                            li.EnsureVisible();
                        }
                        else if (ad.IsDefaultCommunicationsDevice)
                        {
                            li.SubItems.Add(new ListViewItem.ListViewSubItem(li, "Default Communications Device"));
                            li.EnsureVisible();
                        }
                        else
                        {
                            var caption = "Ready";
                            switch (ad.State)
                            {
                                case DeviceState.Disabled:
                                    caption = "Disabled";
                                    imageMod += "d";
                                    break;
                                case DeviceState.Unplugged:
                                    caption = "Not Plugged In";
                                    imageMod += "d";
                                    break;
                                case DeviceState.NotPresent:
                                    caption = "Not Present";
                                    imageMod += "d";
                                    break;
                            }
                            li.SubItems.Add(new ListViewItem.ListViewSubItem(li, caption));
                        }

                        if (ad.State != DeviceState.Unplugged && FavouriteDeviceManager.IsFavouriteDevice(ad))
                            imageMod += "f";

                        if (ad.IsDefaultDevice)
                            imageMod += "e";
                        else if (ad.IsDefaultCommunicationsDevice)
                            imageMod += "c";

                        var imageToGen = imageKey + imageMod;

                        if (!imageList.Images.Keys.Contains(imageToGen))
                        {
                            Image i;
                            using (var icon = extractIcon(ad.IconPath))
                            {
                                i = icon.ToBitmap();
                            }

                            if (ad.State == DeviceState.Disabled || ad.State == DeviceState.Unplugged || ad.State == DeviceState.NotPresent)
                                i = ImageHelper.SetImageOpacity(i, 0.5F) ?? i;

                            using (var g = Graphics.FromImage(i))
                            {
                                if (imageMod.IndexOf('f') >= 0)
                                    g.DrawImage(Resources.f, i.Width - 12, 0);
                                if (imageMod.IndexOf('c') >= 0)
                                    g.DrawImage(Resources.c, i.Width - 12, i.Height - 12);
                                if (imageMod.IndexOf('e') >= 0)
                                    g.DrawImage(Resources.e, i.Width - 12, i.Height - 12);
                            }

                            imageList.Images.Add(imageToGen, i);
                        }

                        if (imageList.Images.IndexOfKey(imageToGen) >= 0)
                            li.ImageKey = imageToGen;
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warn("Device icon render failed: " + ex.Message);
                        li.ImageKey = "unknown";
                    }

                    listView.Items.Add(li);
                }
            }
            finally
            {
                listView.EndUpdate();
            }
        }
    }
}
