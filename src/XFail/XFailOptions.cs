using System.Reflection;

namespace Ohirok.XFail.ReqnrollPlugin
{
    public static class XFailOptions
    {
        private static IXFailExceptionFactory _factory = new DefaultXFailExceptionFactory();

        public static IXFailExceptionFactory ExceptionFactory
        {
            get => _factory;
            set => _factory = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Sets custom exception types for XFail failures.
        /// <para>
        /// <typeparamref name="TUnexpectedPass"/> must have a constructor: (string message)
        /// </para>
        /// <para>
        /// <typeparamref name="TWrongException"/> must have a constructor: (string message, Exception innerException)
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required constructors are not found.
        /// </exception>
        public static void UseExceptionTypes<TUnexpectedPass, TWrongException>()
            where TUnexpectedPass : Exception
            where TWrongException : Exception
        {
            ValidateExceptionType<TUnexpectedPass>(requiresInner: false);
            ValidateExceptionType<TWrongException>(requiresInner: true);

            ExceptionFactory = new SimpleExceptionFactory<TUnexpectedPass, TWrongException>();
        }

        private static void ValidateExceptionType<T>(bool requiresInner) where T : Exception
        {
            var type = typeof(T);
            var paramTypes = requiresInner
                ? new[] { typeof(string), typeof(Exception) }
                : new[] { typeof(string) };

            var ctor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                null,
                paramTypes,
                null);

            if (ctor == null)
            {
                var expected = requiresInner
                    ? "(string message, Exception innerException)"
                    : "(string message)";
                throw new ArgumentException(
                    $"Exception type {type.Name} must have a public constructor {expected}",
                    nameof(T));
            }
        }
    }

    public class XFailUnexpectedPassException(string message) : Exception(message)
    {
    }

    public class XFailWrongException(string message, Exception inner) : Exception(message, inner)
    {
    }

    public interface IXFailExceptionFactory
    {
        Exception UnexpectedPass(string message);
        Exception WrongException(string message, Exception inner);
    }

    public sealed class DefaultXFailExceptionFactory : IXFailExceptionFactory
    {
        public Exception UnexpectedPass(string message) => new XFailUnexpectedPassException(message);
        public Exception WrongException(string message, Exception inner) => new XFailWrongException(message, inner);
    }

    internal class SimpleExceptionFactory<TUnexpectedPass, TWrongException> : IXFailExceptionFactory
    where TUnexpectedPass : Exception
    where TWrongException : Exception
    {
        public Exception UnexpectedPass(string message) =>
            (Exception)Activator.CreateInstance(typeof(TUnexpectedPass), message);

        public Exception WrongException(string message, Exception inner) =>
            (Exception)Activator.CreateInstance(typeof(TWrongException), message, inner);
    }
}
