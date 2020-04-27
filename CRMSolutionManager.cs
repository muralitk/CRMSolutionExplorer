using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using McTools.Xrm.Connection;
using System.Collections.Specialized;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;
using System.ServiceModel;
using System.IO;
using System.Xml.Linq;

namespace CRMSolutionExplorer
{
    public class CRMSolutionManager : IDisposable
    {
        private IOrganizationService Service = null;

        #region Disposable implementation
        private bool disposed = false; // to detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (Service != null)
                        Service = null;
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CRMSolutionManager()
        {
            Dispose(false);
        }
        #endregion

        public CRMSolutionManager(IOrganizationService service)
        {
            this.Service = service;
        }
        public List<Entity> RetrieveSolutions(string solutionName, bool? isManaged)
        {
            var query = new QueryExpression
            {
                EntityName = "solution",
                ColumnSet = new ColumnSet(true),
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.NotNull);

            if(isManaged.HasValue)
                query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, isManaged);

            query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
            if (!string.IsNullOrEmpty(solutionName))
            {
                solutionName = solutionName.Replace("*", "%").Trim();
                query.Criteria.AddCondition("uniquename", ConditionOperator.Like, solutionName);
            }

            query.Orders.Add(new OrderExpression("installedon", OrderType.Descending));

            var request = new RetrieveMultipleRequest();
            request.Query = query;
            try
            {
                var response = (RetrieveMultipleResponse)Service.Execute(request);
                var results = response.EntityCollection.Entities.ToList();
                return results;
            }
            catch
            {
                throw;
            }
        }

        public Entity GetSolutionByName(string uniqueName)
        {
            if (string.IsNullOrEmpty(uniqueName)) return null;

            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet(true),
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
            return Service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        public byte[] ExportSolution(string solutionName, bool isManaged)
        {
            var exportSolutionRequest = new ExportSolutionRequest();
            exportSolutionRequest.Managed = isManaged;
            //give solution unique name
            exportSolutionRequest.SolutionName = solutionName;
            var exportSolutionResponse = (ExportSolutionResponse)Service.Execute(exportSolutionRequest);

            byte[] exportXml = exportSolutionResponse.ExportSolutionFile;
            //string fileName = $@"{folderPlace}\{solutionName}.zip";

            //if (Directory.Exists(folderPlace))
            //    //give path where the solkution file need to store
            //    File.WriteAllBytes(fileName, exportXml);
            //else
            //    throw new Exception($"The folder{fileName} doesn´t exists");
            return exportXml;
        }

        private Tuple<bool, string> IsImportSuccess(IOrganizationService service, Guid importJobId)
        {
            var importJob = new Entity("importjob");
            importJob = service.Retrieve(importJob.LogicalName, importJobId, new ColumnSet(true));
            if (importJob == null) return new Tuple<bool, string>(false, string.Empty);

            var xdoc = XDocument.Parse(importJob["data"].ToString());
            var importedSolutionName = xdoc.Descendants("solutionManifest").Descendants("UniqueName").First().Value;
            bool solutionImportResult = xdoc.Descendants("solutionManifest").Descendants("result").First().FirstAttribute.Value
                == "success" ? true : false;

            return new Tuple<bool, string>(solutionImportResult, importedSolutionName);
        }

        public bool ImportSolution(IOrganizationService service, byte[] fileBytes)
        {
            var request = new ImportSolutionRequest()
            {
                CustomizationFile = fileBytes,
                OverwriteUnmanagedCustomizations = true,
                ImportJobId = Guid.NewGuid()
            };

            try
            {
                var response = (ImportSolutionResponse)service.Execute(request);
                var result = IsImportSuccess(service, request.ImportJobId);

                return result.Item1;
            }
            catch
            {
                throw;
            }
        }

        public bool CloneSolution(string parentSolutionName, string displayName, string versionNumber = "")
        {
            var request = new CloneAsSolutionRequest()
            {
                DisplayName = displayName,
                ParentSolutionUniqueName = parentSolutionName,
            };

            if (!string.IsNullOrEmpty(versionNumber))
                request.VersionNumber = versionNumber;

            try
            {
                var response = (CloneAsSolutionResponse)Service.Execute(request);
                return true;
            }
            catch
            {
                throw;
            }
        }

        public bool ClonePatch(string parentSolutionName, string displayName, string versionNumber)
        {
            if (string.IsNullOrEmpty(parentSolutionName) || string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(versionNumber))
                throw new Exception("Invalid info provied!");

            var request = new CloneAsPatchRequest()
            {
                DisplayName = $"{displayName}-Clone",
                ParentSolutionUniqueName = parentSolutionName,
            };

            if (!string.IsNullOrEmpty(versionNumber))
                request.VersionNumber = versionNumber;

            try
            {
                var response = (CloneAsPatchResponse)Service.Execute(request);
                return true;
            }
            catch 
            {
                throw;
            }
        }

        public string GetNextVersion(string version, bool isClone = true, bool isIncrement = true)
        {
            var ver = new Version(version);
            if (isClone)
                return $"{ver.Major}.{ver.Minor}.{ver.Build + (isIncrement ? 1 : -1)}.{ver.Revision}";
            else
                return $"{ver.Major}.{ver.Minor + (isIncrement ? 1 : -1)}.{ver.Build}.{ver.Revision}";
        }

        public bool UpdateSolution(Guid solutionId, string displayName, string versionNumber)
        {
            if (string.IsNullOrEmpty(versionNumber) && string.IsNullOrEmpty(displayName)) return false;

            var upSolution = new Entity("solution");
            upSolution.Id = solutionId;
            if (!string.IsNullOrEmpty(versionNumber)) upSolution["version"] = versionNumber;
            if (!string.IsNullOrEmpty(displayName)) upSolution["friendlyname"] = displayName;

            Service.Update(upSolution);
            return true;
        }

        public bool DeleteSolution(Guid solutionId)
        {
            if (solutionId == Guid.Empty) return false;

            Service.Delete("solution", solutionId);
            return true;
        }

        public bool PublishSolution(string solutionId, string uniqueName)
        {
            //var xml = $"<importexportxml><solutionmanifest><uniquename>{uniqueName}</uniquename></solutionmanifest></importexportxml>";
            //var xml = $"<importexportxml><solutionmanifest><solutionid>{solutionId.Replace("{", "").Replace("}", "")}</solutionid></solutionmanifest></importexportxml>";
            var xml = $"<importexportxml><solutions><solution>{solutionId.Replace("{", "").Replace("}", "")}</solution></solutions></importexportxml>";
            var pr = new PublishXmlRequest
            {
                ParameterXml = String.Format(xml)
            };
            var result = (PublishXmlResponse) Service.Execute(pr);
            return true;
        }

        public bool PublishAllSolution()
        {
            var pr = new PublishAllXmlRequest();
            var result = (PublishAllXmlResponse) Service.Execute(pr);
            
            return true;
        }

    }
}
