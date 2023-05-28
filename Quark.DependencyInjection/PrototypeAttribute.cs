using System;

namespace Quark.DependencyInjection;

/// <summary>
/// Prototype(都度インスタンス生成)で登録する
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class PrototypeAttribute : Attribute, IDependencyInjectionScope { }
