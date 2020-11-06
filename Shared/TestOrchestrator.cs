using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zebble.Device;

namespace Zebble.Testing
{
    public abstract class TestOrchestrator
    {
        DateTime TestingTime = "2022/01/01 10:00:00".To<DateTime>();

        public virtual void Run()
        {
            LocalTime.RedefineNow(() => TestingTime);
            MakeItFast();

            Thread.Pool.RunOnNewThread(async () =>
            {
                foreach (var test in GetTests())
                {
                    try
                    {
                        await test.Run();

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

        public virtual void MakeItFast()
        {
            Animation.DefaultDuration = Animation.OneFrame;
            Animation.DefaultListItemSlideDuration = Animation.OneFrame;
            Animation.DefaultSwitchDuration = Animation.OneFrame;
        }

        public abstract IEnumerable<UITest> GetTests();
    }
}
