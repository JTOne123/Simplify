﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;
using Simplify.DI;
using Simplify.System;
using Simplify.WindowsServices.CommandLine;
using Simplify.WindowsServices.Jobs;

namespace Simplify.WindowsServices
{
	/// <summary>
	/// Provides class which runs as a windows service and periodically creates a class instances specified in added jobs and launches them in separated thread
	/// </summary>
	public class MultitaskServiceHandler : ServiceBase
	{
		private readonly IList<IServiceJob> _jobsList = new List<IServiceJob>();
		private readonly IDictionary<ICrontabServiceJob, Task> _jobsInWork = new Dictionary<ICrontabServiceJob, Task>();
		private readonly IDictionary<object, ILifetimeScope> _basicJobsInWork = new Dictionary<object, ILifetimeScope>();

		private IServiceJobFactory _serviceJobFactory;
		private ICommandLineProcessor _commandLineProcessor;

		/// <summary>
		/// Initializes a new instance of the <see cref="MultitaskServiceHandler" /> class.
		/// </summary>
		public MultitaskServiceHandler()
		{
			var assemblyInfo = new AssemblyInfo(Assembly.GetCallingAssembly());
			ServiceName = assemblyInfo.Title;
		}

		/// <summary>
		/// Gets or sets the service job factory.
		/// </summary>
		/// <value>
		/// The service job factory.
		/// </value>
		/// <exception cref="ArgumentNullException">value</exception>
		public IServiceJobFactory ServiceJobFactory
		{
			get
			{
				return _serviceJobFactory ?? (_serviceJobFactory = new ServiceJobFactory());
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				_serviceJobFactory = value;
			}
		}

		/// <summary>
		/// Gets or sets the current command line processor.
		/// </summary>
		/// <exception cref="ArgumentNullException"></exception>
		public ICommandLineProcessor CommandLineProcessor
		{
			get
			{
				return _commandLineProcessor ?? (_commandLineProcessor = new CommandLineProcessor());
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				_commandLineProcessor = value;
			}
		}

		/// <summary>
		/// Occurs when exception thrown.
		/// </summary>
		public event ServiceExceptionEventHandler OnException;

		#region Jobs creation

		/// <summary>
		/// Adds the job.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="configurationSectionName">Name of the configuration section.</param>
		/// <param name="invokeMethodName">Name of the invoke method.</param>
		/// <param name="automaticallyRegisterUserType">if set to <c>true</c> then user type T will be registered in DIContainer with transient lifetime.</param>
		public void AddJob<T>(string configurationSectionName = null, string invokeMethodName = "Run",
			bool automaticallyRegisterUserType = false)
			where T : class
		{
			if (automaticallyRegisterUserType)
				DIContainer.Current.Register<T>(LifetimeType.Transient);

			var job = ServiceJobFactory.CreateCrontabServiceJob<T>(configurationSectionName, invokeMethodName);

			job.OnCronTimerTick += OnCronTimerTick;
			job.OnStartWork += OnStartWork;

			_jobsList.Add(job);
		}

		/// <summary>
		/// Adds the service job.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="automaticallyRegisterUserType">if set to <c>true</c> then user type T will be registered in DIContainer with transient lifetime.</param>
		public void AddJob<T>(bool automaticallyRegisterUserType)
			where T : class
		{
			AddJob<T>(null, "Run", automaticallyRegisterUserType);
		}

		/// <summary>
		/// Adds the basic service job.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="automaticallyRegisterUserType">if set to <c>true</c> then user type T will be registered in DIContainer with transient lifetime.</param>
		/// <param name="invokeMethodName">Name of the invoke method.</param>
		public void AddBasicJob<T>(bool automaticallyRegisterUserType = false, string invokeMethodName = "Run")
			where T : class
		{
			if (automaticallyRegisterUserType)
				DIContainer.Current.Register<T>(LifetimeType.Transient);

			var job = ServiceJobFactory.CreateServiceJob<T>(invokeMethodName);

			_jobsList.Add(job);
		}

		#endregion Jobs creation

		/// <summary>
		/// Disposes of the resources (other than memory) used by the <see cref="T:System.ServiceProcess.ServiceBase" />.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				foreach (var jobObject in _basicJobsInWork.Select(item => item.Key as IDisposable))
					jobObject?.Dispose();
			}

			base.Dispose(disposing);
		}

		#region Service process control

		/// <summary>
		/// When implemented in a derived class, executes when a Start command is sent to the service by the Service Control Manager (SCM) or when the operating system starts (for a service that starts automatically). Specifies actions to take when the service starts.
		/// </summary>
		/// <param name="args">Data passed by the start command.</param>
		protected override void OnStart(string[] args)
		{
			foreach (var job in _jobsList)
			{
				job.Start();

				if (!(job is ICrontabServiceJob))
					RunBasicJob(job);
			}

			base.OnStart(args);
		}

		/// <summary>
		/// When implemented in a derived class, executes when a Stop command is sent to the service by the Service Control Manager (SCM). Specifies actions to take when a service stops running.
		/// </summary>
		protected override void OnStop()
		{
			Task.WaitAll(_jobsInWork.Values.ToArray());

			base.OnStop();
		}

		#endregion Service process control

		#region Crontab jobs operations

		private void OnCronTimerTick(object state)
		{
			var job = (ICrontabServiceJob)state;

			if (!job.CrontabProcessor.IsMatching())
				return;

			job.CrontabProcessor.CalculateNextOccurrences();

			lock (_jobsInWork)
				if (_jobsInWork.ContainsKey(job))
					return;

			OnStartWork(state);
		}

		private void OnStartWork(object state)
		{
			var job = (ICrontabServiceJob)state;

			lock (_jobsInWork)
			{
				if (_jobsInWork.ContainsKey(job))
					return;

				_jobsInWork.Add(job, Task.Factory.StartNew(Run, job));
			}
		}

		private void Run(object state)
		{
			var job = (ICrontabServiceJob)state;

			try
			{
				using (var scope = DIContainer.Current.BeginLifetimeScope())
				{
					var jobObject = scope.Container.Resolve(job.JobClassType);

					job.InvokeMethodInfo.Invoke(jobObject, job.IsParameterlessMethod ? null : new object[] { ServiceName });
				}
			}
			catch (Exception e)
			{
				if (OnException != null)
					OnException(new ServiceExceptionArgs(ServiceName, e));
				else
					throw;
			}
			finally
			{
				if (job.Settings.CleanupOnTaskFinish)
					GC.Collect();

				lock (_jobsInWork)
					_jobsInWork.Remove(job);
			}
		}

		#endregion Crontab jobs operations

		/// <summary>
		/// Starts the windows-service.
		/// </summary>
		/// <param name="args">The arguments.</param>
		public bool Start(string[] args = null)
		{
			var commandLineProcessResult = CommandLineProcessor.ProcessCommandLineArguments(args);

			switch (commandLineProcessResult)
			{
				case ProcessCommandLineResult.SkipServiceStart:
					return false;

				case ProcessCommandLineResult.NoArguments:
					ServiceBase.Run(this);
					break;
			}

			return true;
		}

		private void RunBasicJob(IServiceJob job)
		{
			try
			{
				var scope = DIContainer.Current.BeginLifetimeScope();

				var jobObject = scope.Container.Resolve(job.JobClassType);

				job.InvokeMethodInfo.Invoke(jobObject, job.IsParameterlessMethod ? null : new object[] { ServiceName });

				_basicJobsInWork.Add(jobObject, scope);
			}
			catch (Exception e)
			{
				if (OnException != null)
					OnException(new ServiceExceptionArgs(ServiceName, e));
				else
					throw;
			}
		}
	}
}