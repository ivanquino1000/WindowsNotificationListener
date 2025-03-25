using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Devices.Radios;
using Windows.Foundation.Metadata;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using static System.Net.Mime.MediaTypeNames;

// Notification Test Format. 
// Title:  yape: confirmación de pago
// Content: Yape! Esau J. Fuentes M. te envió un pago por S/ 23.50
namespace WindowsNotificationHandler
{
    public class NotificationHandler
    {
        private UserNotificationListener listener;
        public IList<UserNotification> yapeNotifications;
        private HashSet<string> allowedNotificationTitles;
        public NotificationHandler()
        {
            listener = UserNotificationListener.Current;
            yapeNotifications = new List<UserNotification>();
            allowedNotificationTitles = new HashSet<string>
            {
                "pushbullet: test notification",
                "yape: confirmación de pago",
            };

        }
        // Check if the listener is supported 
        public bool isSupported()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
            {
                string SupportedMessage = "Notification Listener Available.\n";
                Debug.WriteLine(SupportedMessage);
                return true;
            }

            else
            {
                string errorMessage = "Notification Listener is not supported on this version of Windows.\n";
                Debug.WriteLine(errorMessage);
                return false;
            }
        }

        public async Task requestNotificationAccess()
        {
            if (!isSupported())
            {
                Environment.Exit(0);
            }
            UserNotificationListenerAccessStatus accessStatus = await listener.RequestAccessAsync();

            if (accessStatus == UserNotificationListenerAccessStatus.Allowed)
            {
                Debug.WriteLine("Notification Listener Allowed!!! \n");
            }
            else
            {
                Debug.WriteLine("client Denied Notification Listener \n");
                //Environment.Exit(0);
            }

        }

        async public Task RequestBackgrounExecutionAccess()
        {
            BackgroundAccessStatus backgroundAcccess = await BackgroundExecutionManager.RequestAccessAsync();
            switch (backgroundAcccess)
            {
                case BackgroundAccessStatus.Unspecified:
                    Debug.WriteLine("BackgroundAccessStatus.Unspecified");
                    break;
                case BackgroundAccessStatus.AlwaysAllowed:
                    Debug.WriteLine("BackgroundAccessStatus.AlwaysAllowed");
                    break;
                case BackgroundAccessStatus.AllowedSubjectToSystemPolicy:
                    Debug.WriteLine("BackgroundAccessStatus.AllowedSubjectToSystemPolicy");
                    break;
                case BackgroundAccessStatus.DeniedBySystemPolicy:
                    Debug.WriteLine("BackgroundAccessStatus.DeniedBySystemPolicy");
                    break;
                case BackgroundAccessStatus.DeniedByUser:
                    Debug.WriteLine("BackgroundAccessStatus.DeniedByUser");
                    break;

            }
            //checks and Place the Background Task
            if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals("UserNotificationChanged")))
            {
                // Specify the background task
                var builder = new BackgroundTaskBuilder()
                {
                    Name = "UserNotificationChanged"
                };

                // Set the trigger for Listener, listening to Toast Notifications
                builder.SetTrigger(new UserNotificationChangedTrigger(NotificationKinds.Toast));

                // Register the task
                builder.Register();
            }
        }

        private class YapeNotification
        {
            public string TitleText { get; set; }
            public string ContentText { get; set; }
            public string ClientName { get; set; }
            public string Amount { get; set; }

            public UserNotification Notification { get; set; }
            public YapeNotification(UserNotification originalNotification)
            {
                this.Notification = originalNotification;
                this.TitleText = getTitle(originalNotification);
                this.ContentText = getContent(originalNotification);
                var clientData = getClient(originalNotification);
                if (clientData != null)
                {
                    this.ClientName = clientData.Item1;
                    this.Amount = clientData.Item2;
                }


            }
            private Tuple<string, string> getClient(UserNotification notification)
            {
                string pattern = @"^Yape!\s+([A-Za-zÁÉÍÓÚáéíóúñÑ\s\.]+)\s+te\s+envió\s+un\s+pago\s+por\s+S\/\s(\d+\.?\d*)";

                // Create a Regex object
                Regex regex = new Regex(pattern);

                // Match the input string against the regex pattern
                Match match = regex.Match(getContent(notification));

                if (match.Success)
                {
                    // Extract name and amount
                    string name = match.Groups[1].Value.Trim();
                    string amount = match.Groups[2].Value.Trim();

                    // Print the result in the desired format
                    Debug.WriteLine($"Yape Notification Metadata  :{name} {amount}");
                    return new Tuple<string, string>(name, amount);
                }
                else
                {
                    Debug.WriteLine("No match found.");
                    return null;
                }
            }
            private string getTitle(UserNotification notification)
            {
                NotificationBinding toastBinding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                IReadOnlyList<AdaptiveNotificationText> textElements = toastBinding.GetTextElements();

                return textElements.FirstOrDefault()?.Text;

            }
            private string getContent(UserNotification notification)
            {
                NotificationBinding toastBinding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                IReadOnlyList<AdaptiveNotificationText> textElements = toastBinding.GetTextElements();

                return string.Join("\n", textElements.Skip(1).Select(t => t.Text));
            }
        }




        public async Task readNotifications()
        {

            IReadOnlyList<UserNotification> userNotifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);

            foreach (UserNotification userNotification in userNotifications)
            {
                if (!isValidNotification(userNotification))
                {
                    continue;
                }

                YapeNotification notification = new YapeNotification(userNotification);

                Debug.WriteLine("ID: " + userNotification.Id);
                Debug.WriteLine(notification.TitleText);
                Debug.WriteLine(notification.ContentText);
                //IList<NotificationBinding> nBindings = userNotification.Notification.Visual.Bindings;

                //foreach (var binding in nBindings)
                //{
                //    var elements = binding.GetTextElements();
                //    foreach (var element in elements)
                //    {
                //        // Assuming the element has a 'text' property
                //        var textElement = element.Text;
                //        Debug.WriteLine("Extracted Text: " + textElement);
                //    }
                //}

            }
        }
        private IList<UInt32> getNotificationIds(IList<UserNotification> notificationList)
        {
            IList<UInt32> notificationIds = new List<uint>();
            foreach (UserNotification notification in notificationList)
            {
                notificationIds.Add(notification.Id);
            }
            return notificationIds;
        }

        private string GetNotificationTitle(UserNotification notification)
        {
            NotificationBinding toastBinding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            IReadOnlyList<AdaptiveNotificationText> textElements = toastBinding.GetTextElements();

            return textElements.FirstOrDefault()?.Text;

        }
        public async Task notificationChangedHandler()
        {
            Debug.WriteLine("Notification Chaged Event Triggered");
            IReadOnlyList<UserNotification> userNotifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);

            //check if notification already exist in List
            IList<UInt32> registeredIds = this.getNotificationIds(this.yapeNotifications);

            foreach (UserNotification userNotification in userNotifications)
            {
                if (!isValidNotification(userNotification))
                {
                    continue;
                }
                // Iterate over current valid Notifications and Returns all IDs


                if (registeredIds.Contains(userNotification.Id))
                {
                    Debug.WriteLine("notification Already Registered: " + userNotification.Id);
                    continue;
                }
                await PlayAudioConfirmation(userNotification);
                storeNotification(userNotification);
            }
            //WriteNotificationsToFile(yapeNotifications, "C:\\Users\\ivan\\Desktop\\notificationListener\notifications.txt");
        }

        public Boolean isValidNotification(UserNotification notification)
        {
            //  Compare notification Date To system
            DateTime currentDateTime = DateTime.Now;
            DateTime notificationDate = notification.CreationTime.Date;


            //if (currentDateTime.ToString("yyyy-MM-dd") != notificationDate.ToString("yyyy-MM-dd"))
            //{
            //    Debug.WriteLine("Invalid Notification - Date: " + notificationDate.ToString("yyyy-MM-dd"));
            //    return false;

            //}
            string titleText = GetNotificationTitle(notification);

            if (allowedNotificationTitles.Contains(titleText.ToLower()))
            {
                return true;
            }
            else
            {
                Debug.WriteLine("Invalid Notification - Title: " + titleText.ToLower());
                return false;
            }

        }
        async private Task PlayAudioConfirmation(UserNotification notification)
        {
            // Play Yape - Notification Sound
            Debug.WriteLine("Audio Confirmation Request");
            var element = new MediaElement();
            
            var folder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets\\Sounds");
            var file = await folder.GetFileAsync("alert.ogg");
            var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
            element.SetSource(stream, "");
            element.Play();
            
            // Play Client Metadata

            YapeNotification ParsedNotification = new YapeNotification(notification);
            MediaElement mediaElement = new MediaElement();
            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();

            SpeechSynthesisStream clientStream = await synth.SynthesizeTextToStreamAsync(ParsedNotification.ClientName + ParsedNotification.Amount);

            // Send the stream to the media object.
            mediaElement.SetSource(clientStream, clientStream.ContentType);
            mediaElement.Play();

        }
        public static void WriteNotificationsToFile(IList<UserNotification> notifications, string fileName)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                foreach (var notification in notifications)
                {
                    // Write the notification data to the file
                    writer.WriteLine($"ID: {notification.Id}");
                    writer.WriteLine(); // Adds a blank line between notifications for readability
                }
            }

            Debug.WriteLine("Notifications written to file successfully.");
        }
        private void storeNotification(UserNotification notification)
        {
            yapeNotifications.Add(notification);
            Debug.WriteLine("Local Notification - Notif saved  : \n" + notification.Id);
        }


    }
}
