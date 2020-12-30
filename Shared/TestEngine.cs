using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Olive;
using Log = Zebble.Device.Log;

namespace Zebble.Testing
{
    public class TestEngine
    {
        public static void Run()
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

            if (types.Any(t => t.Defines<UnderDevelopment>()))
            {
                return types.Where(t => t.Defines<UnderDevelopment>());
            }

            if (types.Any(t => t.Defines<TestCase>()))
            {
                var orderedTypes = types.Where(t => t.Defines<TestCase>())
                    .Select(t => new TypeWithAttribute<TestCase>(t.GetCustomAttribute<TestCase>(), t))
                    .OrderBy(x => x.Attribute.Order).Select(x => x.CurrentType).ToList();

                orderedTypes.AddRange(types.Except(orderedTypes));

                return orderedTypes;
            }

            return types;
        }
    }

    class TypeWithAttribute<T>
    {
        public T Attribute { get; set; }

        public Type CurrentType { get; set; }

        public TypeWithAttribute(T attribute, Type type)
        {
            Attribute = attribute;
            CurrentType = type;

            if (Attribute == null) Attribute = Activator.CreateInstance<T>();
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class UnderDevelopment : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class TestCase : Attribute
    {
        public int Order { get; set; }
    }
}
