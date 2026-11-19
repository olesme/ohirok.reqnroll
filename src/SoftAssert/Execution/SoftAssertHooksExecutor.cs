using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Ohirok.CustomBindings;
using Ohirok.SoftAssert.Core;
using Reqnroll.Bindings.Reflection;
using Reqnroll.BoDi;

namespace Ohirok.SoftAssert.Execution
{
    internal static class SoftAssertHooksExecutor
    {
        private static readonly ConcurrentDictionary<MethodInfo, ParameterInfo[]> _parametersCache = new();
        private static readonly ConcurrentDictionary<MethodInfo, Func<object, object[], object>> _invokers = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo> _registerMethodCache = new();

        private static readonly MethodInfo RegisterTypeAsMethod = typeof(IObjectContainer)
            .GetMethod(nameof(IObjectContainer.RegisterTypeAs));

        public static async Task ExecuteAsync(
            ICustomBindingProvider provider,
            SoftAssertContext ctx,
            string eventKey,
            IObjectContainer container)
        {
            var hooks = provider.GetHooksForEvent(eventKey);

            bool needsStates = false;
            foreach (var hook in hooks)
            {
                if (hook.Filters != null && hook.Filters.Length > 0)
                {
                    needsStates = true;
                    break;
                }
            }

            string[] states = needsStates ? BuildStates(ctx) : null;

            foreach (var hook in hooks)
            {
                if (hook.Filters != null && hook.Filters.Length > 0 && !FilterHelper.ShouldExecute(hook.Filters, states))
                {
                    continue;
                }

                await InvokeHookAsync(hook, ctx, container);
            }
        }

        private static string[] BuildStates(SoftAssertContext ctx)
        {
            int capacity = ctx.AllTags.Length * 2 + ctx.Hashtags.Length * 2 + 10;
            var list = new List<string>(capacity);

            AddTagsToStates(list, ctx.AllTags, '@');
            AddTagsToStates(list, ctx.Hashtags, '#');

            if (ctx.Error != null)
            {
                var exName = ctx.Error.GetType().Name;
                list.Add($"threw:{exName}");
                list.Add($"exception:{exName}");
                list.Add($"ex:{exName}");
                list.Add($"error:{exName}");
                list.Add(exName);
            }

            return [.. list];
        }

        private static void AddTagsToStates(List<string> list, string[] tags, char prefix)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                list.Add(tags[i]);
                list.Add($"{prefix}{tags[i]}");
            }
        }

        private static async Task InvokeHookAsync(CustomHook hook, SoftAssertContext ctx, IObjectContainer container)
        {
            var method = ((RuntimeBindingMethod)hook.Method).MethodInfo;

            object instance = null;
            if (!method.IsStatic)
            {
                instance = ResolveOrCreate(method.DeclaringType, container);
            }

            var parameters = _parametersCache.GetOrAdd(method, m => m.GetParameters());
            var args = ResolveParameters(parameters, ctx, container);

            var invoker = _invokers.GetOrAdd(method, CreateInvoker);
            var result = invoker(instance, args);

            if (result is Task task)
                await task;
        }

        private static Func<object, object[], object> CreateInvoker(MethodInfo method)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            var parameters = method.GetParameters();
            var argExpressions = new Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var argAccess = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                argExpressions[i] = Expression.Convert(argAccess, parameters[i].ParameterType);
            }

            Expression callExpr;
            if (method.IsStatic)
            {
                callExpr = Expression.Call(method, argExpressions);
            }
            else
            {
                var instanceCast = Expression.Convert(instanceParam, method.DeclaringType);
                callExpr = Expression.Call(instanceCast, method, argExpressions);
            }

            if (method.ReturnType == typeof(void))
            {
                callExpr = Expression.Block(callExpr, Expression.Constant(null));
            }
            else if (method.ReturnType != typeof(object))
            {
                callExpr = Expression.Convert(callExpr, typeof(object));
            }

            var lambda = Expression.Lambda<Func<object, object[], object>>(
                callExpr,
                instanceParam,
                argsParam);

            return lambda.Compile();
        }

        private static object ResolveOrCreate(Type type, IObjectContainer container)
        {
            if (!container.IsRegistered(type))
            {
                var genericMethod = _registerMethodCache.GetOrAdd(
                    type,
                    t => RegisterTypeAsMethod.MakeGenericMethod(t, t)
                );
                genericMethod.Invoke(container, new object[] { null });
            }

            return container.Resolve(type);
        }

        private static object[] ResolveParameters(ParameterInfo[] parameters, SoftAssertContext ctx, IObjectContainer container)
        {
            if (parameters.Length == 0)
                return [];

            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var pt = parameters[i].ParameterType;

                if (pt == typeof(SoftAssertContext))
                {
                    args[i] = ctx;
                    continue;
                }

                if (container.IsRegistered(pt))
                {
                    args[i] = container.Resolve(pt);
                }
                else
                {
                    args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
                }
            }
            return args;
        }
    }
}
