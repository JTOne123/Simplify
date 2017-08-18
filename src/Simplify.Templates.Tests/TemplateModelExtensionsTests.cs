﻿using System;
using NUnit.Framework;
using Simplify.Templates.Tests.Models;

namespace Simplify.Templates.Tests
{
	[TestFixture]
	public class TemplateModelExtensionsTests
	{
		private ITemplate _template;

		[SetUp]
		public void Initialize()
		{
			_template = Template.FromString("{ID} {Name} {EMail} {CreationTime}");
		}

		[Test]
		public void Set_NullModel_ReplacesWithNothing()
		{
			// Act
			_template.Model<TestModel>(null).Set();

			// Assert		
			Assert.AreEqual("   ", _template.Get());
		}

		[Test]
		public void Set_NullModelWithWith_ReplacesWithNothing()
		{
			// Act
			_template.Model<TestModel>(null).With(x => x.CreationTime, x => x.ToString("dd.MM.yyyy")).Set();

			// Assert
			Assert.AreEqual("   ", _template.Get());
		}


		[Test]
		public void Set_Model_SetCorrectly()
		{
			// Assign
			var model = new TestModel { CreationTime = new DateTime(2014, 10, 5), Name = "Foo", EMail = "Foo@example.com", ID = 5 };

			// Act
			_template.Model(model).With(x => x.CreationTime, x => x.ToString("dd.MM.yyyy")).Set();

			// Assert
			Assert.AreEqual("5 Foo Foo@example.com 05.10.2014", _template.Get());
		}

		[Test]
		public void Set_EverythingIsNull_SetCorrectly()
		{
			// Assign

			_template = Template.FromString("{ID} {Name} {EMail}");
			var model = new TestModel();

			// Act
			_template.Model(model).Set();

			// Assert
			Assert.AreEqual("  ", _template.Get());
		}

		[Test]
		public void Add_TwoModels_DataCombined()
		{
			// Assign

			_template = Template.FromString("{Name} {EMail}");
			var model = new TestModel { Name = "Test", EMail = "test@test.com" };
			var model2 = new TestModel { Name = "Foo", EMail = "foomail@test.com" };

			// Act

			_template.Model(model).Add();
			_template.Model(model2).Add();

			// Assert
			Assert.AreEqual("TestFoo test@test.comfoomail@test.com", _template.Get());
		}

		[Test]
		public void Set_ModelWithBaseClass_BaseClassFieldsAlsoSet()
		{
			// Assign

			_template = Template.FromString("{ID} {Name}");
			var model = new ChildTestModel { ID = 3, Name = "Hello!" };

			// Act
			_template.Model(model).Set();

			// Assert
			Assert.AreEqual("3 Hello!", _template.Get());
		}

		[Test]
		public void Set_ModelWithPrefix_PrefixAdded()
		{
			// Assign

			_template = Template.FromString("{Model.ID}");
			var model = new ChildTestModel { ID = 3 };

			// Act
			_template.Model(model, "Model").Set();

			// Assert
			Assert.AreEqual("3", _template.Get());
		}
	}
}