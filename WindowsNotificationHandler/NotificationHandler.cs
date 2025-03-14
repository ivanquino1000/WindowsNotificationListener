using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Windows.Foundation.Metadata;
using Windows.Media.Playback;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.UI.Xaml.Documents;

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
            allowedNotificationTitles = new HashSet<string>
            {
                "pushbullet: test notification",
                "yape: confirmación de pago",
            };
            requestNotificationAccess();
        }
        // Check if the listener is working or supported 
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
            Debug.WriteLine("yapeNotifications: \n" + yapeNotifications);
        }

        //event Handler async structure 
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
        public async void readNotifications()
        {

            IReadOnlyList<UserNotification> userNotifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);

            foreach (UserNotification userNotification in userNotifications)
            {
                if (!isValidNotification( userNotification))
                {
                    continue; 
                }
                Debug.WriteLine("ID: " + userNotification.Id);
                IList<NotificationBinding> nBindings = userNotification.Notification.Visual.Bindings;

                foreach (var binding in nBindings)
                {
                    var elements = binding.GetTextElements();
                    foreach (var element in elements)
                    {
                        // Assuming the element has a 'text' property
                        var textElement = element.Text;
                        Debug.WriteLine("Extracted Text: " + textElement);
                    }
                }

            }
        }
        private string GetNotificationText(UserNotification notification, bool getTitle = true)
        {
            NotificationBinding toastBinding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            IReadOnlyList<AdaptiveNotificationText> textElements = toastBinding.GetTextElements();

            if (getTitle)
            {
                return textElements.FirstOrDefault()?.Text;
            }
            else
            {
                return string.Join("\n", textElements.Skip(1).Select(t => t.Text));
            }
        }
        public async Task notificationChangedHandler()
        {

            IReadOnlyList<UserNotification> userNotifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);

            //check if notification already exist in List
            

            foreach (UserNotification userNotification in userNotifications)
            {
                if (!isValidNotification(userNotification))
                {
                    Debug.Print("Non valid notification");
                    continue;
                }

                storeNotification(userNotification);
            }
            //WriteNotificationsToFile(yapeNotifications, "C:\\Users\\ivan\\Desktop\\notificationListener\notifications.txt");
        }
        public Boolean isValidNotification(UserNotification notification)
        {
            //  Compare notification Date To system
            DateTime currentDateTime = DateTime.Now;
            DateTime notificationDate = notification.CreationTime.Date;


            if (currentDateTime.ToString("yyyy-MM-dd") != notificationDate.ToString("yyyy-MM-dd"))
            {
                Debug.WriteLine("Invalid Notification - Date: " + notificationDate.ToString("yyyy-MM-dd"));
                return false;

            }
            string titleText = GetNotificationText(notification, true);

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

    }
}
