﻿using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Simplify.DI.Provider.SimpleInjector
{
	/// <summary>
	/// Simple Injector DI provider lifetime scope implementation
	/// </summary>
	public class SimpleInjectorLifetimeScope : ILifetimeScope
	{
		private readonly Scope _scope;

		/// <summary>
		/// Initializes a new instance of the <see cref="SimpleInjectorLifetimeScope"/> class.
		/// </summary>
		/// <param name="provider">The provider.</param>
		public SimpleInjectorLifetimeScope(SimpleInjectorDIProvider provider)
		{
			Container = provider;

			_scope = AsyncScopedLifestyle.BeginScope(provider.Container);
		}

		/// <summary>
		/// Gets the DI container provider (should be user to resolve types when using scoping).
		/// </summary>
		/// <value>
		/// The DI container provider (should be user to resolve types when using scoping).
		/// </value>
		public IDIContainerProvider Container { get; }

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			_scope.Dispose();
		}
	}
}