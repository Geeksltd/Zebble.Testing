using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zebble;

namespace Zebble.Testing
{
    public abstract class UITest
    {
        static Canvas Highlighter;

        public abstract Task Run();

        // TODO: Build the commands required from 
        // https://github.com/Geeksltd/Pangolin/blob/master/Commands/commands.md

        void Wait(int milliseconds) => Wait(milliseconds.Milliseconds());

        void Wait(TimeSpan time) => Task.Factory.RunSync(() => Task.Delay(time));

        public TView[] AllVisible<TView>() where TView : View
        {
            return View.Root.CurrentDescendants().OfType<TView>().Where(v => v.IsVisibleOnScreen()).ToArray();
        }

        T TryToFind<T>(Func<T> action, string definition) where T : View
        {
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

            throw new Exception("Not found: " + definition);
        }

        protected void Assert(string condition, bool conditionTest)
        {
            if (!conditionTest)
                throw new Exception("Assertion failed: " + condition);
        }

        protected void Expect(string text, bool caseSensitive = false)
        {
            TryToFind(() => AllVisible<TextControl>().FirstOrDefault(x => x.Text.Contains(text, caseSensitive)), text);
        }

        protected void SwipeCarousel(Direction direction = Direction.Left)
        {
            var carousel = Find<Zebble.Plugin.Carousel>();
            if (direction == Direction.Left) carousel.Next(animate: false);
            else if (direction == Direction.Right) carousel.Previous(animate: false);
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

        /// <summary>
        /// Wait for Toast message
        /// </summary>
        /// <param name="text">Expected message</param>
        protected async Task WaitForPopup(string text)
        {
            var attempts = 20;

            while (attempts > 0)
            {
                if (View.Root.AllChildren<Toast>().Any(x => x.Message.Contains(text, false))) return;

                await Task.Delay(50);
                attempts--;
            }

            throw new Exception("No Toast message containing the phrase '" + text + "' was found on the screen.");
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
        protected Task Touch(string button, bool caseSensitive = false)
        {
            try
            {
                var arg = new TouchEventArgs(View.Root, new Point(0, 0), 1);
                AllVisible<Button>().First(x => x.Text.Contains(button, caseSensitive) || x.Id.Contains(button, caseSensitive)).RaiseTouched(arg);

                return Task.CompletedTask;
            }
            catch (Exception)
            {
                throw new Exception($"No button containing the phrase {button} was found.");
            }
        }

        protected T Find<T>() where T : View => TryToFind<T>(() => AllVisible<T>().FirstOrDefault(), typeof(T).Name);

        protected Task Swipe(Direction direction)
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
        /// Go to the page
        /// </summary>
        /// <typeparam name="T">Page class that inherit from NavBarPage class</typeparam>
        protected async Task GoTo<T>() where T : NavBarPage
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
        protected void TypeIn(string id, string content)
        {
            var item = ById<TextInput>(id);
            item.Text(content);
            Thread.Pool.RunOnNewThread(() => item.UserTextChanged.Raise());
        }

        /// <summary>
        /// Delay
        /// </summary>
        /// <param name="delay">milli-seconds</param>
        protected void Delay(int delay = 1000) => Wait(delay);
    }
}