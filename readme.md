

FitBit
https://vipin-johney.medium.com/fitbit-authentication-xamarin-forms-5900ed8e9caa
https://dev.fitbit.com/


Google Fit

Activity: This captures any activity that a user does. It can include health and fitness activities like running or swimming, meditation, and sleep.
Body Measurement: This captures common data related to the body. It includes capturing a user’s weight, or a user’s basal metabolic rate, among other data types.
Cycle Tracking: This captures menstrual cycles and related data points, such as the binary result of an ovulation test.
Nutrition: This captures hydration and nutrition data types. The former represents how much water a user drank in a single drink. The latter includes many optional fields, from calories to sugar and magnesium that record which nutrients the user consumed.
Sleep: This captures interval data related to a user’s length and type of sleep.
Vitals: This captures essential information about the user’s general health. It includes everything from blood glucose to body temperature and blood oxygen saturation.

https://developer.android.com/guide/health-and-fitness/health-connect


Apple Health

https://learn.microsoft.com/en-us/xamarin/ios/platform/healthkit


Watch Heart Rate and start workout
https://developer.apple.com/forums/thread/6549

* Subscribe to updates
HealthKit - HKStatisticsQuery - InitialHandler vs UpdateHandler for subscriptions
https://stackoverflow.com/questions/30306653/google-fit-add-activity-programmatically
https://stackoverflow.com/questions/58777453/reading-the-heart-rate-from-wear-os-watch-using-google-fit-from-a-paired-app

https://developers.google.com/fit/android/sensors


An explicit App ID.
A Provisioning Profile associated with that explicit App ID and with Health Kit permissions.
An Entitlements.plist with a com.apple.developer.healthkit property of type Boolean set to Yes.
An Info.plist whose UIRequiredDeviceCapabilities key contains an entry with the String value healthkit.
The Info.plist must also have appropriate privacy-explanation entries: a String explanation for the key NSHealthUpdateUsageDescription if the app is going to write data and a String explanation for the key NSHealthShareUsageDescription if the app is going to read Health Kit data.


## 3rd Party Libraries
* [Shiny .NET](https://shinylib.net)
* [GoogleCast](https://github.com/kakone/GoogleCast)


https://github.com/OvalMoney/react-native-fitness