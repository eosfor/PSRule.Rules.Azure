// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Management.Automation;
using PSRule.Rules.Azure.Configuration;
using PSRule.Rules.Azure.Data;
using PSRule.Rules.Azure.Data.Bicep;
using PSRule.Rules.Azure.Data.Network;
using PSRule.Rules.Azure.Data.Template;
using PSRule.Rules.Azure.Pipeline;
using PSRule.Rules.Azure.Pipeline.Output;

namespace PSRule.Rules.Azure.Runtime
{
    /// <summary>
    /// Helper methods exposed to PowerShell.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Parse and reformat the expression by removing whitespace.
        /// </summary>
        public static string CompressExpression(string expression)
        {
            return !IsTemplateExpression(expression) ? expression : ExpressionParser.Parse(expression).AsString();
        }

        /// <summary>
        /// Look for literals and variable usage.
        /// </summary>
        public static bool HasLiteralValue(string expression)
        {
            return !IsTemplateExpression(expression) ||
                TokenStreamValidator.HasLiteralValue(ExpressionParser.Parse(expression));
        }

        /// <summary>
        /// Check it the string is an expression.
        /// </summary>
        public static bool IsTemplateExpression(string expression)
        {
            return ExpressionParser.IsExpression(expression);
        }

        /// <summary>
        /// Expand resources from a parameter file and linked template/ bicep files.
        /// </summary>
        public static PSObject[] GetResources(string parameterFile, int timeout)
        {
            var context = GetContext();
            var linkHelper = new TemplateLinkHelper(context, PSRuleOption.GetWorkingPath(), true);
            var link = linkHelper.ProcessParameterFile(parameterFile);
            if (link == null)
                return null;

            return IsBicep(link.TemplateFile) ?
                GetBicepResources(link.TemplateFile, link.ParameterFile, null, timeout) :
                GetTemplateResources(link.TemplateFile, link.ParameterFile, context);
        }

        /// <summary>
        /// Expand resources from a bicep file.
        /// </summary>
        public static PSObject[] GetBicepResources(string bicepFile, PSCmdlet commandRuntime, int timeout)
        {
            return GetBicepResources(bicepFile, null, commandRuntime, timeout);
        }

        /// <summary>
        /// Get the linked template path.
        /// </summary>
        public static string GetMetadataLinkPath(string parameterFile, string templateFile)
        {
            return TemplateLinkHelper.GetMetadataLinkPath(parameterFile, templateFile);
        }

        /// <summary>
        /// Evaluate NSG rules.
        /// </summary>
        public static INetworkSecurityGroupEvaluator GetNetworkSecurityGroup(PSObject[] securityRules)
        {
            var builder = new NetworkSecurityGroupEvaluator();
            builder.With(securityRules);
            return builder;
        }

        /// <summary>
        /// Get resource type information.
        /// </summary>
        public static ResourceProviderType[] GetResourceType(string providerNamespace, string resourceType)
        {
            var resourceProviderHelper = new ResourceProviderHelper();
            return resourceProviderHelper.GetResourceType(providerNamespace, resourceType);
        }

        /// <summary>
        /// Get the last element in the sub-resource name by splitting the name by '/' separator.
        /// </summary>
        /// <param name="resourceName">The sub-resource name to split.</param>
        /// <returns>The name of the sub-resource.</returns>
        public static string GetSubResourceName(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
                return resourceName;

            var parts = resourceName.Split('/');
            return parts[parts.Length - 1];
        }

        #region Helper methods

        private static PSObject[] GetTemplateResources(string templateFile, string parameterFile, PipelineContext context)
        {
            var helper = new TemplateHelper(
                context
            );
            return helper.ProcessTemplate(templateFile, parameterFile, out _);
        }

        private static bool IsBicep(string path)
        {
            return Path.GetExtension(path) == ".bicep";
        }

        private static PSObject[] GetBicepResources(string templateFile, string parameterFile, PSCmdlet commandRuntime, int timeout)
        {
            var context = GetContext(commandRuntime);
            var bicep = new BicepHelper(
                context,
                timeout
            );
            return bicep.ProcessFile(templateFile, parameterFile);
        }

        private static PipelineContext GetContext(PSCmdlet commandRuntime = null)
        {
            var option = PSRuleOption.FromFileOrDefault(PSRuleOption.GetWorkingPath());
            var context = new PipelineContext(option, commandRuntime != null ? new PSPipelineWriter(option, commandRuntime) : null);
            return context;
        }

        #endregion Helper methods
    }
}
