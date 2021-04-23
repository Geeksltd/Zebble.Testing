using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble.Testing
{
    public abstract class UITest : IBaseUITest
    {
        static Canvas Highlighter;

        public abstract Task Run();

        // TODO: Build the commands required from 
        // https://github.com/Geeksltd/Pangolin/blob/master/Commands/commands.md

        public TView[] AllVisible<TView>() where TView : View
        {
            return View.Root.CurrentDescendants().OfType<TView>().Where(v => v.IsVisibleOnScreen()).ToArray();
        }

        public T Find<T>() where T : View => TryToFind<T>(() => AllVisible<T>().FirstOrDefault(), typeof(T).Name);

        public Task Swipe(Direction direction)
        {
            try
            {
                var arg = new SwipedEventArgs(View.Root, direction, 20);
                View.Root.RaiseSwipped(arg);

                return Task.CompletedTask;
            }
            catch (Exception)
            {
                throw new Exception($"Can't swipe to the direction \"{direction}\"");
            }
        }

        /// <summary>
        /// Delay
        /// </summary>
        /// <param name="delay">milli-seconds</param>
        public void Delay(int delay = 1000) => Wait(delay);

        void Wait(int milliseconds) => Wait(milliseconds.Milliseconds());

        void Wait(TimeSpan time) => Task.Factory.RunSync(() => Task.Delay(time));

        T TryToFind<T>(Func<T> action, string definition) where T : View
        {
#if ANDROID
            Delay(50);
#endif
            AwaitNavigationCompletion();

            var attempts = 300;

            while (attempts > 0)
            {
                var result = action();
                if (result != null)
                {
                    Visualize(result);
                    return result;
                }

                Wait(Animation.OneFrame);
                attempts--;
            }

            if (Debugger.IsAttached) Debugger.Break();

            throw new Exception("Not found: " + definition);
        }

        protected void Assert(string condition, Func<bool> conditionTest)
        {
            AwaitNavigationCompletion();

            var attempts = 300;

            while (attempts > 0)
            {
                if (conditionTest()) return;
                Wait(Animation.OneFrame);
                attempts--;
            }

            if (Debugger.IsAttached) Debugger.Break();

            throw new Exception("Assertion failed: " + condition);
        }

        protected void Expect(string text, bool caseSensitive = false)
        {
            TryToFind(() => AllVisible<TextControl>().FirstOrDefault(x => x.Text.Contains(text, caseSensitive)), text);
        }

        protected void Tap(string buttonText, int delay = 100) => Tap(FindByText(buttonText), delay);

        void AwaitNavigationCompletion()
        {
            while (Nav.IsNavigating) Delay(50);
        }

        protected void Break() => throw new Exception("Breakpoint...");

        protected void Tap(View view, int delay = 100)
        {
            AwaitNavigationCompletion();

            view.RaiseTouched(new TouchEventArgs(view, new Point(), 1));
            view.RaiseTapped();
            Delay(delay);
        }

        protected void Tap<T>(int delay = 100) where T : View
        {
            var item = TryToFind(() =>
            {
                var match = AllVisible<T>().ToArray();
                if (match.None()) return null;
                else if (match.IsSingle()) return match[0];
                else return null;
            }, "Not found the view of type " + nameof(T));

            Tap(item, delay);
        }

        protected void TapExcept(string groupId, string text, int delay = 100)
        {
            var item = TryToFind(() =>
            {
                var match = AllVisible<TextView>().Where(x => x.Text != text && x.Id == groupId).ToArray();
                if (match.None()) return null;
                return match[0];
            }, text);

            Tap(item, delay);
        }

        /// <summary>
        /// Wait for Toast or Alert message
        /// </summary>
        /// <param name="text">Expected message</param>
        protected async Task WaitForPopup(string text, bool isAlert = false, int attempts = 20)
        {
            while (attempts > 0)
            {
                if (isAlert)
                {
                    if (View.Root.AllChildren<AlertDialog>().Any(x => x.AllChildren<TextView>().Any(t => t.Text.Contains(text, false)))) return;
                }
                else
                {
                    if (View.Root.AllChildren<Toast>().Any(x => x.Message.Contains(text, false))) return;
                }

                await Task.Delay(50);
                attempts--;
            }

            throw new Exception("No Toast or Alert message containing the phrase '" + text + "' was found on the screen.");
        }

        TextView FindByText(string text)
        {
            return TryToFind(() =>
            {
                var match = AllVisible<TextView>().Where(x => x.Text == text).ToArray();
                if (match.None()) return null;
                if (match.IsSingle()) return match[0];
                else return null;
            }, text);
        }

        protected View ById(string id) => ById<View>(id);

        protected T ById<T>(string id) where T : View
        {
            return TryToFind(() =>
            {
                var match = AllVisible<T>().Where(x => x.Id == id).ToArray();
                if (match.None()) return null;
                if (match.IsSingle()) return match[0];
                else return null;
            }, "Id not found: " + id);
        }

        protected T ById<T>(int position, string id = null) where T : View
        {
            return TryToFind(() =>
            {
                T[] match;

                if (id == null) match = AllVisible<T>().ToArray();
                else match = AllVisible<T>().Where(x => x.Id == id).ToArray();

                if (match.None()) return null;
                return match[position];

            }, id != null ? "Id not found: " + id : "View not found: " + nameof(T));
        }

        static async void Visualize(View view)
        {
            if (Highlighter == null)
            {
                await View.Root.Add(Highlighter = new Canvas().Opacity(.7f).Border(3, color: Colors.Red));
                Nav.Navigating.Handle(() => Highlighter.Hide());
            }

            var mine = Guid.NewGuid().ToString();
            Highlighter.Data["For"] = mine;

            Highlighter.ChangeInBatch(() => Highlighter
               .X(view.CalculateAbsoluteX() - 10)
               .Y(view.CalculateAbsoluteY() - 10)
               .Width(view.ActualWidth + 20)
               .Height(view.ActualHeight + 20)
               .Visible());

            Task.Delay(3.Seconds()).ContinueWith(x =>
            {
                if (Highlighter.Data["For"].ToString() == mine) Highlighter.Hide();
            }).RunInParallel();

            await Highlighter.BringToFront();
        }

        /// <summary>
        /// Fire tapped event of the button
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <param name="caseSensitive">Case sensitive</param>
        protected Task Tap<T>(string itemId, bool caseSensitive = false) where T : View
        {
            try
            {
                AllVisible<T>().First(x => x.Id.Contains(itemId, caseSensitive)).RaiseTapped();

                return Task.CompletedTask;
            }
            catch (Exception)
            {
                throw new Exception($"No button with the ID {itemId} was found.");
            }
        }

        /// <summary>
        /// Fire touch event of the button
        /// </summary>
        /// <param name="button">Button text or ID</param>
        /// <param name="caseSensitive">Case sensitive</param>
        protected Task Touch<TButton>(string button, bool caseSensitive = false) where TButton : Button
        {
            try
            {
                var arg = new TouchEventArgs(View.Root, new Point(0, 0), 1);
                AllVisible<TButton>().First(x => x.Text.Contains(button, caseSensitive) || x.Id.Contains(button, caseSensitive)).RaiseTouched(arg);

                return Task.CompletedTask;
            }
            catch (Exception)
            {
                throw new Exception($"No button containing the phrase {button} was found.");
            }
        }

        /// <summary>
        /// Go to the page
        /// </summary>
        /// <typeparam name="T">Page class that inherit from NavBarPage class</typeparam>
        protected async Task GoTo<T>() where T : Page
        {
            try
            {
                await Nav.Go<T>();
            }
            catch (Exception)
            {
                throw new Exception("Page not found");
            }
        }

        /// <summary>
        /// Set a value for the selected input
        /// </summary>        
        protected void TypeIn(string id, string content, bool shouldSubmitEventExc = false)
        {
            var item = ById<TextInput>(id);
            item.Text(content);
            item.UserTextChanged.Raise();
            Thread.Pool.RunOnNewThread(async () =>
            {
                if (shouldSubmitEventExc) await item.UserTextChangeSubmitted.Raise();
                else await item.UserTextChanged.Raise();
            });
        }

        /// <summary>
        /// Scroll the page to the specefic view
        /// </summary>
        /// <param name="id">id of the target view</param>
        protected void ScrollToView(string id)
        {
            var allViews = View.Root.AllDescendents();
            var view = allViews.Where(x => x.Id == id).FirstOrDefault();
            if (view == null) return;

            var sc = allViews.OfType<ScrollView>().FirstOrDefault();
            if (sc != null)
                sc.ScrollToView(view, animate: false);
        }

        /// <summary>
        /// Scroll the page to the specefic position
        /// </summary>
        /// <param name="yOffset">Y offset</param>
        protected async void ScrollToY(int yOffset)
        {
            await View.Root.AllDescendents().OfType<ScrollView>().FirstOrDefault(sc => sc.Id == "BodyScroller")?.ScrollTo(yOffset);
        }

        /// <summary>
        /// Waiting for a view until it has shown
        /// </summary>
        /// <param name="view">target view</param>
        protected void WaitFor(View view) => WaitFor<View>(x => x.IsShown, -1);

        /// <summary>
        /// Waiting for a view until the condition returns true or timeout reached
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="timeout"></param>
        protected void WaitFor(Func<View, bool> predicate, int timeout = 5000) => WaitFor<View>(predicate, timeout);

        /// <summary>
        /// Waiting for a view until the condition returns true or timeout reached
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="timeout"></param>
        protected void WaitFor<T>(Func<T, bool> predicate, int timeout = 5000) where T : View
        {
            int index = 0;
#if ANDROID
            Delay(50);
#else
            Delay(20);
#endif
            AwaitNavigationCompletion();

            while (true)
            {
                if (timeout != -1 && index * ((int)Animation.OneFrame.TotalMilliseconds) > timeout) break;
                if (AllVisible<T>().Where(predicate).Any()) return;

                Wait(Animation.OneFrame);

                index++;
            }

            if (Debugger.IsAttached) Debugger.Break();

            throw new Exception($"Waiting timeout for: {typeof(T).Name}");
        }

        static TaskCompletionSource<bool> TapWaiting;

        /// <summary>
        /// Raising the Tap event of a view and wait for it to complete
        /// </summary>
        /// <param name="buttonText"></param>
        /// <param name="delay"></param>
        protected Task WaitAndTap(string buttonText, int delay = 200)
        {
            return WaitAndTap(FindByText(buttonText), delay);
        }

        /// <summary>
        /// Raising the Tap event of a view and wait for it to complete
        /// </summary>
        /// <param name="view"></param>
        /// <param name="delay"></param>
        protected Task WaitAndTap(View view, int delay = 200)
        {
            TapWaiting = new TaskCompletionSource<bool>();

            Delay(delay);

            view.Tapped.Raise(new TouchEventArgs(view, new Point(), 1)).ContinueWith(t =>
            {
                if (t.IsCompleted) TapWaiting.TrySetResult(true);
            }).RunInParallel();

            return TapWaiting.Task;
        }
    }
}
