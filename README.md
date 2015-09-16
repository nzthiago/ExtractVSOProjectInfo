# Extract Visual Studio Online Project Info
This project connects to the **Visual Studio Online REST API** to read information about user commits and team builds.

It was written fairly quickly so pull requests with improvements and fixes welcome!

## Running the sample
From Visual Studio create a local **AppSettingsSecrets.config** file (not included in this code) with the app settings one folder up from the solution folder. Here's an example content for the AppSettingsSecrets.config file:

```xml
<?xml version="1.0" encoding="utf-8" ?>
  <appSettings>
    <add key="user" value="<your VSO alternate login>" />
    <add key="password" value="<your VSO alternate password>" />
    <add key="url" value="<your VSO account>.visualstudio.com" />
</appSettings>
```

Modify the values in that file with your own VSO account and credentials. Steps for setting up the credentails can be found here:
VSO Alternate Authentication Credential 
https://www.visualstudio.com/integrate/get-started/auth/overview
