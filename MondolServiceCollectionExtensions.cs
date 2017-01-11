using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MondolServiceCollectionExtensions
    {
        /// <summary>
        /// DUMP服务列表
        /// </summary>
        public static string Dump(this IServiceCollection services)
        {
            var sevList = new List<Tuple<string, string>>();
            foreach (var sev in services)
            {
                sevList.Add(new Tuple<string, string>(sev.Lifetime.ToString(), sev.ServiceType.FullName));
            }
            sevList.Sort((x, y) =>
            {
                var cRs = string.CompareOrdinal(x.Item1, y.Item1);
                return cRs != 0 ? cRs : string.CompareOrdinal(x.Item2, y.Item2);
            });

            return string.Join("\r\n", sevList.Select(p => $"{p.Item2} - {p.Item1}"));
        }

        /// <summary>
        /// 确保当前注册服务的依赖关系是正确的
        /// </summary>
        public static void AssertDependencyValid(this IServiceCollection services)
        {
            var ignoreTypes = new[]
            {
                "Microsoft.AspNetCore.Mvc.Internal.MvcRouteHandler",
                "Microsoft.AspNetCore.Razor.Runtime.TagHelpers.TagHelperDescriptorResolver"
            };

            foreach (var svce in services)
            {
                if (svce.Lifetime == ServiceLifetime.Singleton)
                {
                    //确保Singleton的服务不能依赖Scoped的服务
                    if (svce.ImplementationType != null)
                    {
                        var svceType = svce.ImplementationType;
                        if (ignoreTypes.Contains(svceType.FullName))
                            continue;

                        var ctors = svceType.GetConstructors();
                        foreach (var ctor in ctors)
                        {
                            var paramLst = ctor.GetParameters();
                            foreach (var param in paramLst)
                            {
                                var paramType = param.ParameterType;
                                var paramTypeInfo = paramType.GetTypeInfo();
                                if (paramTypeInfo.IsGenericType)
                                {
                                    if (paramType.ToString().StartsWith("System.Collections.Generic.IEnumerable`1"))
                                    {
                                        paramType = paramTypeInfo.GetGenericArguments().First();
                                        paramTypeInfo = paramType.GetTypeInfo();
                                    }
                                }
                                if (paramType == typeof(IServiceProvider))
                                    continue;

                                ServiceDescriptor pSvce;
                                if (paramTypeInfo.IsGenericType)
                                {
                                    //泛型采用模糊识别，可能有遗漏
                                    var prefix = Regex.Match(paramType.ToString(), @"^[^`]+`\d+\[").Value;
                                    pSvce = services.FirstOrDefault(p => p.ServiceType.ToString().StartsWith(prefix));
                                }
                                else
                                {
                                    pSvce = services.FirstOrDefault(p => p.ServiceType == paramType);
                                }
                                if (pSvce == null)
                                    throw new InvalidProgramException($"服务 {svceType.FullName} 的构造方法引用了未注册的服务 {paramType.FullName}");
                                if (pSvce.Lifetime == ServiceLifetime.Scoped)
                                    throw new InvalidProgramException($"Singleton的服务 {svceType.FullName} 的构造方法引用了Scoped的服务 {paramType.FullName}");
                            }
                        }
                    }
                }
            }
        }
    }
}
