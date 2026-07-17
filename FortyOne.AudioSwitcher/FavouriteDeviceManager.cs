using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AudioSwitcher.AudioApi;

namespace FortyOne.AudioSwitcher
{
    public static class FavouriteDeviceManager
    {
        public delegate void FavouriteDeviceIDsChangedEventHandler(List<Guid> ids);

        // HashSet for O(1) membership; List preserves order for quick-switch cycling
        private static readonly HashSet<Guid> FavouriteDeviceSet = new HashSet<Guid>();
        private static readonly List<Guid> FavouriteDeviceOrder = new List<Guid>();

        public static int FavouriteDeviceCount
        {
            get { return FavouriteDeviceOrder.Count; }
        }

        public static int FavouritePlaybackDeviceCount
        {
            get
            {
                return FavouriteDeviceOrder.Count(id =>
                {
                    var device = AudioDeviceManager.Controller.GetDevice(id, DeviceState.All);
                    return device != null && device.IsPlaybackDevice;
                });
            }
        }

        public static ReadOnlyCollection<Guid> FavouriteDevices
        {
            get { return new ReadOnlyCollection<Guid>(FavouriteDeviceOrder); }
        }

        public static event FavouriteDeviceIDsChangedEventHandler FavouriteDevicesChanged;

        public static bool LoadFavouriteDevices(List<Guid> favouriteIDs)
        {
            return LoadFavouriteDevices(favouriteIDs != null ? favouriteIDs.ToArray() : new Guid[0]);
        }

        public static bool LoadFavouriteDevices(Guid[] favouriteIDs)
        {
            FavouriteDeviceSet.Clear();
            FavouriteDeviceOrder.Clear();

            if (favouriteIDs == null)
                return true;

            foreach (var id in favouriteIDs)
            {
                if (id == Guid.Empty)
                    continue;

                // Keep favourites even if device is temporarily missing so they survive sleep/replug
                if (FavouriteDeviceSet.Add(id))
                    FavouriteDeviceOrder.Add(id);
            }

            return true;
        }

        public static bool IsFavouriteDevice(IDevice ad)
        {
            return ad != null && FavouriteDeviceSet.Contains(ad.Id);
        }

        public static bool IsFavouriteDevice(Guid id)
        {
            return FavouriteDeviceSet.Contains(id);
        }

        public static Guid AddFavouriteDevice(Guid id)
        {
            if (id == Guid.Empty || !FavouriteDeviceSet.Add(id))
                return Guid.Empty;

            FavouriteDeviceOrder.Add(id);
            FireFavouriteDeviceChanged();
            return id;
        }

        public static Guid RemoveFavouriteDevice(Guid id)
        {
            if (!FavouriteDeviceSet.Remove(id))
                return Guid.Empty;

            FavouriteDeviceOrder.Remove(id);
            FireFavouriteDeviceChanged();
            return id;
        }

        private static void FireFavouriteDeviceChanged()
        {
            if (FavouriteDevicesChanged != null)
                FavouriteDevicesChanged(new List<Guid>(FavouriteDeviceOrder));
        }

        public static IDevice GetNextFavouritePlaybackDevice(IDevice device)
        {
            var nextDeviceId = GetNextFavouritePlaybackDeviceId(device != null ? device.Id : Guid.Empty);
            return AudioDeviceManager.Controller.GetDevice(nextDeviceId, DeviceState.All);
        }

        public static Guid GetNextFavouritePlaybackDeviceId(Guid deviceId)
        {
            if (FavouriteDeviceOrder.Count == 0)
                return Guid.Empty;

            var index = 0;
            if (deviceId != Guid.Empty)
            {
                var found = FavouriteDeviceOrder.IndexOf(deviceId);
                if (found >= 0)
                    index = (found + 1) % FavouriteDeviceOrder.Count;
            }

            var i = index;
            do
            {
                var id = FavouriteDeviceOrder[i % FavouriteDeviceOrder.Count];
                var ad = AudioDeviceManager.Controller.GetDevice(id, DeviceState.All);
                i = (i + 1) % FavouriteDeviceOrder.Count;

                if (ad == null || ad.State != DeviceState.Active)
                    continue;

                if (ad.DeviceType == DeviceType.Playback)
                    return id;
            } while (i != index);

            return Guid.Empty;
        }
    }
}
