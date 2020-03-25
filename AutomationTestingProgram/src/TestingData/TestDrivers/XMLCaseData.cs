﻿// <copyright file="XMLCaseData.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AutomationTestingProgram.TestingData.TestDrivers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using AutomationTestingProgram.AutomationFramework;
    using AutomationTestingProgram.Helper;
    using AutomationTestSetFramework;
    using TestingDriver;

    /// <summary>
    /// The XML Driver to get data from an xml.
    /// </summary>
    public class XMLCaseData : ITestCaseData
    {
        /// <summary>
        /// The stack to read/excecute for the test set.
        /// </summary>
        private readonly Stack<XmlNode> testStack = new Stack<XmlNode>();

        /// <summary>
        /// Determines if the current stack layer for test set should execute.
        /// </summary>
        private readonly Stack<bool> performStack = new Stack<bool>();

        /// <inheritdoc/>
        public string TestArgs { get; set; }

        /// <inheritdoc/>
        public string Name { get; } = "XML";

        /// <summary>
        /// Gets or sets the information for the test set.
        /// </summary>
        private XmlNode TestFlow { get; set; }

        /// <summary>
        /// Gets or sets the xml file containing the XML Data.
        /// </summary>
        private XmlDocument XMLDataFile { get; set; } = null;

        /// <summary>
        /// Gets or sets the xml file containing the test set/case/steps.
        /// </summary>
        private XmlDocument XMLDocObj { get; set; } = null;

        /// <summary>
        /// Gets or sets the ammount of times this should be ran.
        /// </summary>
        private int ShouldExecuteAmountOfTimes { get; set; } = 1;

        /// <summary>
        /// Gets or sets the ammount of times the test case has ran.
        /// </summary>
        private int ExecuteCount { get; set; } = 0;

        /// <inheritdoc/>
        public void SetUp()
        {
            if (File.Exists(this.TestArgs))
            {
                this.XMLDocObj = new XmlDocument();
                this.XMLDocObj.Load(this.TestArgs);
                this.TestFlow = this.XMLDocObj.GetElementsByTagName("TestCaseFlow")[0];

                string dataFile = Environment.GetEnvironmentVariable("dataFile");
                if (dataFile == string.Empty || dataFile == null)
                {
                    if (this.XMLDocObj.GetElementsByTagName("DataFile").Count > 0)
                    {
                        dataFile = this.XMLDocObj.GetElementsByTagName("DataFile")[0].InnerText;
                        if (File.Exists(dataFile))
                        {
                            this.XMLDataFile = new XmlDocument();
                            this.XMLDataFile.Load(dataFile);
                        }
                        else
                        {
                            Logger.Error("XML File could not be found!");
                        }
                    }
                }
            }
            else
            {
                Logger.Error("XML File could not be found!");
            }
        }

        /// <inheritdoc/>
        public ITestCase SetUpTestCase(string testCaseName, bool performAction = true)
        {
            ITestCase testCase = null;

            // get the list of testcases
            XmlNode testCases = this.XMLDocObj.GetElementsByTagName("TestCases")[0];

            // Find the appropriate testcase;
            foreach (XmlNode node in testCases.ChildNodes)
            {
                if (node.Name == "TestCase" && XMLHelper.ReplaceIfToken(node.Attributes["id"].Value, this.XMLDataFile) == testCaseName)
                {
                    int repeat = 1;
                    string name = "TestCase";

                    this.TestFlow = node;

                    // since this will not be running, there is no need to add to stack.
                    if (performAction)
                    {
                        this.AddNodesToStack(this.TestFlow);
                    }

                    if (InformationObject.RespectRepeatFor && node.Attributes["repeatFor"] != null)
                    {
                        repeat = int.Parse(node.Attributes["repeatFor"].Value);
                    }

                    this.ShouldExecuteAmountOfTimes = repeat;

                    if (node.Attributes["name"] != null)
                    {
                        name = node.Attributes["name"].Value;
                    }

                    testCase = new TestCase()
                    {
                        Name = name,
                        ShouldExecuteVariable = performAction,
                    };
                    return testCase;
                }
            }

            return testCase;
        }

        /// <inheritdoc/>
        public bool ExistNextTestStep()
        {
            return this.testStack.Count > 0 || this.ShouldExecuteAmountOfTimes > this.ExecuteCount + 1;
        }

        /// <inheritdoc/>
        public ITestStep GetNextTestStep()
        {
            ITestStep testStep = null;

            // reached end of loop, check if should loop again.
            if (this.testStack.Count == 0 && this.ShouldExecuteAmountOfTimes > this.ExecuteCount)
            {
                this.AddNodesToStack(this.TestFlow);
                this.ExecuteCount += 1;
            }

            testStep = this.RunIfTestStepLayer();

            return testStep;
        }

        /// <summary>
        /// Adds all the child nodes in the outer most layer to the stack in reverse order.
        /// </summary>
        /// <param name="currentNode">the layer to add.</param>
        /// <param name="performAction">Whether or not the nodes should execute their test cases.</param>
        private void AddNodesToStack(XmlNode currentNode, bool performAction = true)
        {
            for (int i = currentNode.ChildNodes.Count - 1; i >= 0; i--)
            {
                this.testStack.Push(currentNode.ChildNodes[i]);
                this.performStack.Push(performAction);
            }
        }

        private ITestStep RunIfTestStepLayer(bool performAction = true)
        {
            ITestStep testStep = null;
            XmlNode currentNode;

            while (this.testStack.Count > 0 && testStep == null)
            {
                currentNode = this.testStack.Pop();
                performAction = this.performStack.Pop();

                if (currentNode.Name == "If")
                {
                    this.RunThenElseLayer(currentNode, performAction);
                }
                else if (currentNode.Name == "RunTestStep")
                {
                    testStep = this.FindTestStep(XMLHelper.ReplaceIfToken(currentNode.InnerText, this.XMLDataFile), performAction);
                }
                else
                {
                    Logger.Warn($"We currently do not deal with this: {currentNode.Name}");
                }
            }

            return testStep;
        }

        /// <summary>
        /// This function will go through the list of steps and run the appropriate test step if found.
        /// </summary>
        /// <param name="testStepID"> The ID of the test step to run. </param>
        /// <param name="performAction"> Perfoms the action. </param>
        /// <returns>0 if pass. >=1 if fail.</returns>
        private ITestStep FindTestStep(string testStepID, bool performAction = true)
        {
            ITestStep testStep = null;

            // get the list of testSteps
            XmlNode testSteps = this.XMLDocObj.GetElementsByTagName("TestSteps")[0];

            // Find the appropriate test steps
            foreach (XmlNode innerNode in testSteps.ChildNodes)
            {
                if (innerNode.Name != "#comment" && XMLHelper.ReplaceIfToken(innerNode.Attributes["id"].Value, this.XMLDataFile) == testStepID)
                {
                    testStep = this.BuildTestStep(innerNode, performAction);
                    return testStep;
                }
            }

            Logger.Warn($"Sorry, we didn't find a test step that matched the provided ID: {testStepID}");
            return testStep;
        }

        private ITestStep BuildTestStep(XmlNode testStepNode, bool performAction = true)
        {
            TestStep testStep = null;
            string name = XMLHelper.ReplaceIfToken(testStepNode.Attributes["name"].Value, this.XMLDataFile);

            // initial value is respectRunAODAFlag
            // if we respect the flag, and it is not found, then default value is false.
            bool runAODA = InformationObject.RespectRunAODAFlag;
            if (runAODA)
            {
                if (testStepNode.Attributes["runAODA"] != null)
                {
                    runAODA = bool.Parse(testStepNode.Attributes["runAODA"].Value);
                }
                else
                {
                    runAODA = false;
                }
            }

            // populate runAODAPageName. Deault is Not provided.
            string runAODAPageName = "Not provided.";
            if (runAODA)
            {
                if (testStepNode.Attributes["runAODAPageName"] != null)
                {
                    runAODAPageName = XMLHelper.ReplaceIfToken(testStepNode.Attributes["runAODAPageName"].Value, this.XMLDataFile);
                }
            }

            // log is true by default.
            bool log = true;
            if (testStepNode.Attributes["log"] != null)
            {
                log = bool.Parse(testStepNode.Attributes["log"].Value);
            }

            Logger.Debug($"Test step '{name}': runAODA->{runAODA} runAODAPageName->{runAODAPageName} log->{log}");

            testStep = ReflectiveGetter.GetEnumerableOfType<TestStep>()
                .Find(x => x.Name.Equals(testStepNode.Name));

            if (testStep == null)
            {
                Logger.Error($"Was not able to find the provided test action '{testStepNode}'.");
            }
            else
            {
                string namePrepender = this.ExecuteCount > 0 ? $"{this.ExecuteCount}" : $"";

                for (int index = 0; index < testStepNode.Attributes.Count; index++)
                {
                    testStepNode.Attributes[index].InnerText = XMLHelper.ReplaceIfToken(testStepNode.Attributes[index].InnerText, this.XMLDataFile);
                    testStep.Arguments.Add(testStepNode.Attributes[index].Name, testStepNode.Attributes[index].InnerText);
                }

                testStep.Name = name;
                testStep.ShouldLog = log;
                testStep.ShouldExecuteVariable = performAction;
                testStep.RunAODA = runAODA;
                testStep.RunAODAPageName = runAODAPageName;
                testStep.TestStepNumber = this.ExecuteCount * this.TestFlow.ChildNodes.Count;
            }

            return testStep;
        }

        /// <summary>
        /// This function parses the if flow and starts executing. Used in both Test Set and Test Case.
        /// </summary>
        /// <param name="ifXMLNode"> XML Node that has the if block. </param>
        /// <param name="performAction"> Perfoms the action. </param>
        private void RunThenElseLayer(XmlNode ifXMLNode, bool performAction = true)
        {
            bool ifCondition = false;

            // we check condition if we have to perfom this action.
            if (performAction)
            {
                string elementXPath = XMLHelper.ReplaceIfToken(ifXMLNode.Attributes["elementXPath"].Value, this.XMLDataFile);
                string condition = ifXMLNode.Attributes["condition"].Value;

                ITestingDriver.ElementState state = condition == "EXIST" ? ITestingDriver.ElementState.Visible : ITestingDriver.ElementState.Invisible;

                ifCondition = InformationObject.TestAutomationDriver.CheckForElementState(elementXPath, state);
            }

            // inside the testCaseFlow, you can only have either RunTestCase element or an If element.
            foreach (XmlNode ifSection in ifXMLNode.ChildNodes)
            {
                switch (ifSection.Name)
                {
                    case "Then":
                        // we run this test case only if performAction is true, and the condition for the element has passed.
                        this.AddNodesToStack(ifSection, performAction && ifCondition);
                        break;
                    case "ElseIf":
                        // we check the condition if performAction is true and the previous if condition was false.
                        // we can only run the test case if performAction is true, previous if condition was false, and the current if condition is true.
                        bool secondIfCondition = false;

                        if (performAction && !ifCondition)
                        {
                            string elementXPath = XMLHelper.ReplaceIfToken(ifXMLNode.Attributes["elementXPath"].Value, this.XMLDataFile);
                            string condition = ifSection.Attributes["condition"].Value;

                            ITestingDriver.ElementState state = condition == "EXIST" ? ITestingDriver.ElementState.Visible : ITestingDriver.ElementState.Invisible;

                            secondIfCondition = InformationObject.TestAutomationDriver.CheckForElementState(elementXPath, state);
                        }

                        this.AddNodesToStack(ifSection, performAction && !ifCondition && secondIfCondition);

                        // update ifCondition to reflect if elseIf was run
                        ifCondition = !ifCondition && secondIfCondition;
                        break;
                    case "Else":
                        // at this point, we only run this action if performAction is true and the previous ifCondition was false.
                        this.AddNodesToStack(ifSection, performAction && !ifCondition);
                        break;
                    default:
                        Logger.Warn($"We currently do not deal with this. {ifSection.Name}");
                        break;
                }
            }
        }
    }
}
