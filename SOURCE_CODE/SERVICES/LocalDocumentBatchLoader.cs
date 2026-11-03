using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    internal sealed class LocalDocumentBatchResult
    {
        public int ReadyDocuments { get; set; }
        public int DuplicateSources { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    internal static class LocalDocumentBatchLoader
    {
        // Stage is called on the caller's synchronization context, so the shared review queue can bind to WinForms.
        // There is deliberately no business-save service in this batch path.
        internal static async Task<LocalDocumentBatchResult> LoadAsync(IList<LocalDocumentCandidate> files,
            Func<string,IProgress<string>,CancellationToken,Task<List<DocumentImportDraft>>> extract,
            Func<List<DocumentImportDraft>,int> stage,IProgress<string> progress,CancellationToken token)
        {
            var result=new LocalDocumentBatchResult();
            for(int index=0;index<files.Count;index++)
            {
                token.ThrowIfCancellationRequested();
                var file=files[index];int current=index+1;
                progress?.Report("Reading selected file "+current+" of "+files.Count+": "+file.FileName+". No records saved.");
                try
                {
                    if(!file.CanReview||!LocalDocumentDiscoveryService.StillMatchesDiscovery(file))
                    {
                        file.Status="Changed, linked or unavailable — scan again";file.CanReview=false;file.Selected=false;
                        result.Errors.Add(file.FileName+": source changed or is unavailable; scan again.");continue;
                    }
                    var fileProgress=progress==null?null:new Progress<string>(s=>progress.Report("File "+current+"/"+files.Count+": "+s));
                    var drafts=await extract(file.FullPath,fileProgress,token);
                    token.ThrowIfCancellationRequested();
                    if(drafts.Count==0)throw new InvalidOperationException("No readable document was extracted from this file.");
                    int count=stage(drafts);
                    if(count==0){result.DuplicateSources++;file.Status="Already in review queue";}
                    else{result.ReadyDocuments+=count;file.Status="Added to review queue ("+count+" documents)";}
                    file.CanReview=false;file.Selected=false;
                }
                catch(OperationCanceledException){file.Status="Extraction cancelled — retry";throw;}
                catch(Exception ex)
                {
                    Serilog.Log.Warning("Drive-discovered document extraction failed with {ErrorType}",ex.GetType().Name);
                    string reason=ex is InvalidOperationException?ex.Message:"Unreadable or unsupported file. Check the source and retry.";
                    file.Status="Needs attention: "+reason;result.Errors.Add(file.FileName+": "+reason);
                }
            }
            return result;
        }
    }
}
