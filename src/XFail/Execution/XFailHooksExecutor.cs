using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Ohirok.CustomBindings;
using Reqnroll.Bindings.Reflection;
using Reqnroll.BoDi;

namespace Ohirok.XFail.ReqnrollPlugin.Execution
{
    internal static class XFailHooksExecutor
    {
        private static readonly ConcurrentDictionary<MethodInfo, ParameterInfo[]> _parametersCache = new();
        private static readonly ConcurrentDictionary<MethodInfo, Func<object, object[], object>> _invokers = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo> _registerMethodCache = new();

        private static readonly MethodInfo RegisterTypeAsMethod = typeof(IObjectContainer)
            .GetMethod(nameof(IObjectContainer.RegisterTypeAs));

        public static async Task ExecuteAsync(
            ICustomBindingProvider provider,
            XFailScenarioContext ctx,
            string eventKey,
            IObjectContainer container,
            string[] tags)
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

            string[] states = needsStates ? BuildStates(ctx, tags) : null;

            foreach (var hook in hooks)
            {
                if (hook.Filters != null && hook.Filters.Length > 0 && !FilterHelper.ShouldExecute(hook.Filters, states))
                    continue;

                await InvokeHookAsync(hook, ctx, container);
            }
        }

        private static string[] BuildStates(XFailScenarioContext ctx, string[] scenarioTags)
        {
            var list = new List<string>(scenarioTags.Length * 2 + 20);

            foreach (var t in scenarioTags)
            {
                list.Add(t);
                list.Add($"@{t}");
            }

            foreach (var t in scenarioTags)
            {
                var value = ExtractXFailTagValue(t);
                if (value != null)
                    list.Add(value);
            }

            list.Add(ctx.Passed ? "passed" : "failed");
            list.Add(ctx.Mode == XFailMode.XFail ? "xfail" : "afail");

            if (ctx.Exception != null)
            {
                var exName = ctx.Exception.GetType().Name;
                list.Add($"threw:{exName}");
                list.Add($"exception:{exName}");
                list.Add($"ex:{exName}");
                list.Add($"error:{exName}");
                list.Add(exName);
            }

            return [.. list];
        }

        private static string ExtractXFailTagValue(string tag)
        {
            int sep = tag.IndexOf(':');
            if (sep < 0) return null;
            var prefix = tag.Substring(0, sep);
            if (EqIgnCase(prefix, "xfail") || EqIgnCase(prefix, "expectfail") || EqIgnCase(prefix, "expect-fail") ||
                EqIgnCase(prefix, "afail") || EqIgnCase(prefix, "allowfail") || EqIgnCase(prefix, "allow-fail"))
                return tag.Substring(sep + 1);
            return null;
        }

        private static async Task InvokeHookAsync(CustomHook hook, XFailScenarioContext ctx, IObjectContainer container)
        {
            var method = ((RuntimeBindingMethod)hook.Method).MethodInfo;

            object instance = null;
            if (!method.IsStatic)
                instance = ResolveOrCreate(method.DeclaringType, container);

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
                callExpr = Expression.Call(method, argExpressions);
            else
            {
                var instanceCast = Expression.Convert(instanceParam, method.DeclaringType);
                callExpr = Expression.Call(instanceCast, method, argExpressions);
            }

            if (method.ReturnType == typeof(void))
                callExpr = Expression.Block(callExpr, Expression.Constant(null));
            else if (method.ReturnType != typeof(object))
                callExpr = Expression.Convert(callExpr, typeof(object));

            return Expression.Lambda<Func<object, object[], object>>(
                callExpr, instanceParam, argsParam).Compile();
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

        private static object[] ResolveParameters(ParameterInfo[] parameters, XFailScenarioContext ctx, IObjectContainer container)
        {
            if (parameters.Length == 0)
                return [];

            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var pt = parameters[i].ParameterType;

                if (pt == typeof(XFailScenarioContext))
                {
                    args[i] = ctx;
                    continue;
                }

                if (container.IsRegistered(pt))
                    args[i] = container.Resolve(pt);
                else
                    args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
            return args;
        }

        private static bool EqIgnCase(string a, string b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if ((a[i] | 0x20) != (b[i] | 0x20)) return false;
            return true;
        }
    }
}
