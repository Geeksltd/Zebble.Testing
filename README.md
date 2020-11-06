# Zebble.Testing
Automated Testing for Zebble Applications


## Set up
To add UI Testing to your Zebble project, add the `Zebble.Testing` nuget package. Then in `StartUp.cs`, add the following:

```csharp
protected internal virtual bool IsTestMode() => true; // Set to `false` to run the app in normal mode

...

public async Task Launch()
{
     ...
     
     TestEngine.Run();
}

```


## Running a single test
During the development time you will be often focusing on one test at a time.
When you run the application in test mode, by default all tests will be executed. This is not ideal when your focus is just the test at hand.

To execute only one test, add the `[UnderDevelopment]` attribute to your test class. This will tell the engine to ignore all the other tests and only run that one.

```csharp
[UnderDevelopment]
public class LoginTest: UITest
{
   ...
}
```



## Running the tests



## Context/State setup
...
