using System;
using System.ComponentModel;

namespace FxApi
{
    /// <summary>
    /// The `EventUtil` class provides extension methods for safely raising events and invoking delegates.
    /// Delegates are used to define event handlers in C#, and are commonly used to raise events in .NET applications.
    /// These methods simplify event handling by checking for null handlers before attempting to raise 
    /// events or invoke delegates, preventing potential `NullReferenceException` errors. 
    /// This promotes a cleaner and more robust event-driven workflow.
    /// </summary>
    public static class EventUtil
    {
        /// <summary>
        /// Raises the specified `EventHandler` safely, checking for a null handler before invocation.
        /// <param name="handler">The `EventHandler` to be raised.</param>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        public static void Raise(this EventHandler handler, object sender, EventArgs e)
        {
            // Check if the handler is not null.
            if (handler != null)
            {
                // If the handler is not null, invoke it with the provided sender and event arguments.
                handler(sender, e);
            }
        }

        /// <summary>
        /// Raises the specified `EventHandler` safely, using `EventArgs.Empty` if no event arguments are provided.
        /// <param name="handler">The `EventHandler` to be raised.</param>
        /// <param name="sender">The object that raised the event.</param>
        /// </summary>
        public static void Raise(this EventHandler handler, object sender)
        {
            // Call the main `Raise` method with `EventArgs.Empty` as the event arguments.
            Raise(handler, sender, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the specified generic `EventHandler<T>` safely, checking for a null handler before invocation.
        /// <typeparam name="T">The type of the event arguments.</typeparam>
        /// <param name="handler">The generic `EventHandler<T>` to be raised.</param>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments of type `T`.</param>
        /// </summary>
        public static void Raise<T>(this EventHandler<T> handler, object sender, T e) where T : EventArgs
        {
            // Check if the handler is not null.
            if (handler != null)
            {
                // If the handler is not null, invoke it.
                handler(sender, e);
            }
        }

        /// <summary>
        /// Raises the specified `PropertyChangedEventHandler` safely, checking for a null handler before invocation.
        /// <param name="handler">The `PropertyChangedEventHandler` to be raised.</param>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">`PropertyChangedEventArgs` containing information about the property change.</param>
        /// </summary>
        public static void Raise(this PropertyChangedEventHandler handler, object sender, PropertyChangedEventArgs e)
        {
            // Check if the handler is not null.
            if (handler != null)
            {
                // If the handler is not null, invoke it.
                handler(sender, e);
            }
        }

        /// <summary>
        /// Raises the specified `PropertyChangedEventHandler` safely, creating `PropertyChangedEventArgs` from the provided property name.
        /// <param name="handler">The `PropertyChangedEventHandler` to be raised.</param>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="propertyName">The name of the property that has changed.</param>
        /// </summary>
        public static void Raise(this PropertyChangedEventHandler handler, object sender, string propertyName)
        {
            // Call the main `Raise` method for `PropertyChangedEventHandler`, creating new `PropertyChangedEventArgs` using the property name.
            Raise(handler, sender, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Safely invokes the specified `Action` delegate, checking for a null delegate before invocation.
        /// <param name="handler">The `Action` delegate to be invoked.</param>
        /// </summary>
        public static void Raise(this Action handler)
        {
            // Check if the delegate is not null.
            if (handler != null)
            {
                // If the delegate is not null, invoke it.
                handler();
            }
        }

        /// <summary>
        /// Safely invokes the specified generic `Action<T>` delegate, checking for a null delegate before invocation.
        /// <typeparam name="T">The type of the argument for the delegate.</typeparam>
        /// <param name="handler">The generic `Action<T>` delegate to be invoked.</param>
        /// <param name="arg1">The argument to be passed to the delegate.</param>
        /// </summary>
        public static void Raise<T>(this Action<T> handler, T arg1)
        {
            // Check if the delegate is not null.
            if (handler != null)
            {
                // If the delegate is not null, invoke it with the provided argument.
                handler(arg1);
            }
        }

        /// <summary>
        /// Safely invokes the specified generic `Action<T1, T2>` delegate, checking for a null delegate before invocation.
        /// <typeparam name="T1">The type of the first argument for the delegate.</typeparam>
        /// <typeparam name="T2">The type of the second argument for the delegate.</typeparam>
        /// <param name="handler">The generic `Action<T1, T2>` delegate to be invoked.</param>
        /// <param name="arg1">The first argument to be passed to the delegate.</param>
        /// <param name="arg2">The second argument to be passed to the delegate.</param>
        /// </summary
        public static void Raise<T1, T2>(this Action<T1, T2> handler, T1 arg1, T2 arg2)
        {
            // Check if the delegate is not null.
            if (handler != null)
            {
                // If the delegate is not null, invoke it with the provided arguments.
                handler(arg1, arg2);
            }
        }
    }
}