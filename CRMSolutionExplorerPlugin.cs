﻿using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace CRMSolutionExplorer
{
    // Do not forget to update version number and author (company attribute) in AssemblyInfo.cs class
    // To generate Base64 string for Images below, you can use https://www.base64-image.de/
    [Export(typeof(IXrmToolBoxPlugin)),
        ExportMetadata("Name", "CRM Solution Explorer"),
        ExportMetadata("Description", "This plugin will help you to do some basic tasks related to MS CRM Solutions. You can do Patch/Merge/Update/Delete Patches, Copy solution across environments and Publish All Solutions!"),
        // Please specify the base64 content of a 32x32 pixels image
        ExportMetadata("SmallImageBase64", "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"),
        // Please specify the base64 content of a 80x80 pixels image
        ExportMetadata("BigImageBase64", "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"),
        ExportMetadata("BackgroundColor", "White"),
        ExportMetadata("PrimaryFontColor", "Black"),
        ExportMetadata("SecondaryFontColor", "Blue")]
    public class CRMSolutionExplorerPlugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new PluginControl();
        }

        /// <summary>
        /// Constructor 
        /// </summary>
        public CRMSolutionExplorerPlugin()
        {
            // If you have external assemblies that you need to load, uncomment the following to 
            // hook into the event that will fire when an Assembly fails to resolve
            // AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolveEventHandler);
        }

        /// <summary>
        /// Event fired by CLR when an assembly reference fails to load
        /// Assumes that related assemblies will be loaded from a subfolder named the same as the Plugin
        /// For example, a folder named Sample.XrmToolBox.MyPlugin 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            Assembly loadAssembly = null;
            Assembly currAssembly = Assembly.GetExecutingAssembly();

            // base name of the assembly that failed to resolve
            var argName = args.Name.Substring(0, args.Name.IndexOf(","));

            // check to see if the failing assembly is one that we reference.
            List<AssemblyName> refAssemblies = currAssembly.GetReferencedAssemblies().ToList();
            var refAssembly = refAssemblies.Where(a => a.Name == argName).FirstOrDefault();

            // if the current unresolved assembly is referenced by our plugin, attempt to load
            if (refAssembly != null)
            {
                // load from the path to this plugin assembly, not host executable
                string dir = Path.GetDirectoryName(currAssembly.Location).ToLower();
                string folder = Path.GetFileNameWithoutExtension(currAssembly.Location);
                dir = Path.Combine(dir, folder);

                var assmbPath = Path.Combine(dir, $"{argName}.dll");

                if (File.Exists(assmbPath))
                {
                    loadAssembly = Assembly.LoadFrom(assmbPath);
                }
                else
                {
                    throw new FileNotFoundException($"Unable to locate dependency: {assmbPath}");
                }
            }

            return loadAssembly;
        }
    }
}