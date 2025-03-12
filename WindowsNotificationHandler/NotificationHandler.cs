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
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace WindowsNotificationHandler
{
    public class NotificationHandler
    {
        private UserNotificationListener listener;
        public IList<UserNotification> targetNotifications;
        private HashSet<string> allowedNotificationTitles;
        public NotificationHandler()
        {
            listener = UserNotificationListener.Current;
            allowedNotificationTitles = new HashSet<string>
            {
                "pushbullet: test notification",
                "yape: confirmacion de pago",
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
        public async Task notificationChangedHandler()
        {
            IReadOnlyList<UserNotification> userNotifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);

            foreach (UserNotification userNotification in userNotifications)
            {
                if (!isValidNotification(userNotification))
                {
                    Debug.Print("Non valid notification");
                    continue;
                }

                storeNotification(userNotification);
            }
            //WriteNotificationsToFile(targetNotifications, "C:\\Users\\ivan\\Desktop\\notificationListener\notifications.txt");
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
            targetNotifications.Add(notification);
            Debug.WriteLine("targetNotifications: \n" + targetNotifications);
        }
        public async Task RequestNotificationAccessAsync()
        {
            if (!isSupported())
            {
                // Handle the case where notifications aren't supported (you can inform the user or provide an alternative flow)
                Debug.WriteLine("Notification Listener is not supported.");
                return; // Prevent further action if not supported
            }

            try
            {
                // Make sure this runs on the UI thread
                var accessStatus = await listener.RequestAccessAsync();

                switch (accessStatus)
                {
                    case UserNotificationListenerAccessStatus.Allowed:
                        Debug.WriteLine("Notification Listener Allowed!!! \n");
                        break;
                    case UserNotificationListenerAccessStatus.Denied:
                        Debug.WriteLine("Client Denied Notification Listener \n");
                        await listener.RequestAccessAsync();
                        // You can show a message or guide the user to manually enable access via settings
                        break;
                    case UserNotificationListenerAccessStatus.Unspecified:
                        Debug.WriteLine("Notification Access Request Unspecified. Show UI to retry.");
                        // You could prompt the user with an option to open the settings page
                        break;
                    default:
                        Debug.WriteLine("Unexpected access status.");
                        break;
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., user denied access or other unforeseen errors)
                Debug.WriteLine($"Error requesting notification access: {ex.Message}");
            }
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
            listener = UserNotificationListener.Current;
            Debug.Print("Notifications Changed Event: \n");
            IReadOnlyList<UserNotification> userNotifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);

            foreach (UserNotification userNotification in userNotifications)
            {
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
        public Boolean isValidNotification(UserNotification notification)
        {

            string titleText = GetNotificationText(notification, true);

            if (allowedNotificationTitles.Contains(titleText))
            {
                return true;
            }
            else
            {
                Debug.Print("Notification not allowed: ", titleText);
                return false;
            }

        }
    }
}
