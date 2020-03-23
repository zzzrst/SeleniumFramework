﻿// <copyright file="ChooseCollection.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AutomationTestingProgram.AutomationFramework
{
    using AutomationTestingProgram.TestAutomationDriver;

    /// <summary>
    /// This class executes the action of getting to an organization.
    /// </summary>
    public class ChooseCollection : TestStep
    {
        /// <inheritdoc/>
        public override string Name { get; set; } = "ChooseCollection";

        /// <inheritdoc/>
        public override void Execute()
        {
            base.Execute();
            string collectionSearchField = this.Arguments["collectionSearchField"];
            string collectionName = this.Arguments["collectionName"];

            string collectionDropDown = "//*[@aria-label='Choose a collection activate']";
            string collectionSearchBarXPath = "//*[@aria-label='Choose a collection']";
            string collectionElementXpath = $"//*[contains(text(), \"{collectionName}\")]";

            InformationObject.TestAutomationDriver.RefreshWebPage();

            if (!InformationObject.TestAutomationDriver.CheckForElementState(collectionElementXpath, ITestAutomationDriver.ElementState.Visible))
            {
                InformationObject.TestAutomationDriver.ClickElement(collectionDropDown);
                InformationObject.TestAutomationDriver.PopulateElement(collectionSearchBarXPath, collectionSearchField);
                InformationObject.TestAutomationDriver.ClickElement(collectionElementXpath);
                InformationObject.TestAutomationDriver.WaitForLoadingSpinner();
            }
        }
    }
}
