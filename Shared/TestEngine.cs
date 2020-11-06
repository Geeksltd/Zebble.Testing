using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Zebble.Device;

namespace Zebble.Testing
{
    public class TestEngine
    {
        internal static void Run()
        {
            TestContext.Activate();

            Thread.Pool.RunOnNewThread(async () =>
            {
                foreach (var test in GetTests())
                {
                    try
                    {
                        var testCase = Activator.CreateInstance(test) as UITest;
                        await testCase.Run();

                        Log.Success($"Test \"{ test.GetType().Name }\" ran successfully");
                        await Task.Delay(1.Seconds());
                    }
                    catch (Exception ex)
                    {
                        // TODO: Report failed test via Firebase
                        await Alert.Show($"Test failed: \"{ test.GetType().Name }\"\n\n{ex.Message}");
                        return;
                    }
                }

                await Alert.Show("TESTS COMPLETED");
            });
        }

        static IEnumerable<Type> GetTests()
        {
            var types = UIRuntime.GetEntryAssembly().GetTypes().Where(t => t.InhritsFrom(typeof(UITest)));

            if (types.Any(t => t.Defines<UnderDevelopment>())) return types.Where(t => t.Defines<UnderDevelopment>());
            else return types;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class UnderDevelopment : Attribute { }
}
