using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class LocalDocumentDiscoveryTests
    {
        public static string Run(string fixtureRoot)
        {
            string root=Path.GetFullPath(fixtureRoot);
            if(!Path.GetFileName(root).StartsWith("ServoERP-DriveDiscoveryQA-",StringComparison.Ordinal))throw new InvalidOperationException("Use an explicitly owned filesystem QA fixture.");
            var passes=new List<string>();
            var result=LocalDocumentDiscoveryService.ScanDirectory(root,"",true,null,CancellationToken.None,20000);
            Require(result.Files.Count==6,"Supported recursive file discovery did not match the fixture.");
            Require(result.Files.Any(f=>f.FileName=="invoice.PDF")&&result.Files.Any(f=>f.FileName=="quote.xlsx")&&result.Files.Any(f=>f.FileName=="scan.png"),"Case-insensitive format support or nested discovery failed.");
            Require(!result.Files.Any(f=>f.FileName.StartsWith("~$")),"Excel lock file was included.");
            passes.Add("PASS recursive PDF/JPG/PNG/XLSX metadata discovery; uppercase extensions and Excel lock files handled");
            Require(result.Files.Single(f=>f.FileName=="oversized.pdf").CanReview==false&&result.Files.Single(f=>f.FileName=="empty.pdf").CanReview==false,"Oversized/empty files were eligible for extraction.");
            passes.Add("PASS oversized and empty files remain visible with explicit ineligible status");
            Require(result.LinksSkipped==1&&result.SystemFoldersSkippedCount==1&&!result.LimitReached,"Link loop or system/cache skip accounting failed.");
            passes.Add("PASS junction loop skipped without traversal; optional system/cache exclusions counted");
            var filtered=LocalDocumentDiscoveryService.ScanDirectory(root,"INVOICE",true,null,CancellationToken.None,20000);
            Require(filtered.Files.Count==1&&filtered.Files[0].FileName=="invoice.PDF","Filename filtering failed.");
            var capped=LocalDocumentDiscoveryService.ScanDirectory(root,"",true,null,CancellationToken.None,2);
            Require(capped.LimitReached&&capped.Files.Count==2&&capped.Summary.StartsWith("Partial scan"),"Partial scan limit was hidden.");
            passes.Add("PASS filename filter and explicitly reported partial-result limits");
            var missing=LocalDocumentDiscoveryService.ScanDirectory(Path.Combine(root,"missing"),"",true,null,CancellationToken.None,20000);
            Require(missing.UnavailableLocations==1&&missing.Files.Count==0,"Unavailable location aborted discovery incorrectly.");
            var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;
            try{LocalDocumentDiscoveryService.ScanDirectory(root,"",true,null,cancelled.Token,20000);}catch(OperationCanceledException){stopped=true;}
            Require(stopped,"Cancellation was ignored.");passes.Add("PASS unavailable location handling and cancellation before enumeration");
            bool rejected=false;try{LocalDocumentDiscoveryService.ValidateDriveRoot(root);}catch(InvalidOperationException){rejected=true;}Require(rejected,"Non-root path was accepted as an entire drive.");
            rejected=false;try{LocalDocumentDiscoveryService.ValidateDriveRoot(@"\\server\share\");}catch(InvalidOperationException){rejected=true;}Require(rejected,"Network share was accepted as local drive.");
            passes.Add("PASS local drive-root validation rejects folders and UNC/network inputs");
            var file=result.Files.Single(f=>f.FileName=="invoice.PDF");Require(LocalDocumentDiscoveryService.StillMatchesDiscovery(file),"Unchanged source rejected.");
            File.AppendAllText(file.FullPath,"changed");Require(!LocalDocumentDiscoveryService.StillMatchesDiscovery(file),"Changed source was not rejected before extraction.");
            passes.Add("PASS source metadata rechecked before extraction; changed files require another scan");
            var batchFiles=new[]{"scan.png","quote.xlsx","receipt.jpg"}.Select(n=>Candidate(Path.Combine(root,"nested",n))).ToList();
            var staged=new List<DocumentImportDraft>();int calls=0;
            var batch=LocalDocumentBatchLoader.LoadAsync(batchFiles,(path,progress,token)=>
            {
                calls++;if(path.EndsWith("quote.xlsx",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("QA unreadable source");
                return Task.FromResult(new List<DocumentImportDraft>{new DocumentImportDraft{DocumentNumber=Path.GetFileName(path),FileHash=path.EndsWith(".png")?new string('b',64):new string('c',64)}});
            },drafts=>{staged.AddRange(drafts);return drafts.Count;},null,CancellationToken.None).GetAwaiter().GetResult();
            Require(calls==3&&batch.ReadyDocuments==2&&batch.Errors.Count==1&&staged.Count==2&&staged.All(d=>!d.ReviewConfirmed),"A failed extraction discarded successes or auto-approved a document.");
            Require(!batchFiles[0].CanReview&&batchFiles[1].CanReview&&!batchFiles[2].CanReview,"Batch statuses lost successful/failed retry state.");
            passes.Add("PASS batch failure isolation: successes before/after a bad file remain unapproved and available for review");
            var cancelBatch=new CancellationTokenSource();int stagedBeforeCancel=0,cancelCalls=0;stopped=false;
            try
            {
                LocalDocumentBatchLoader.LoadAsync(new[]{"scan.png","receipt.jpg"}.Select(n=>Candidate(Path.Combine(root,"nested",n))).ToList(),(path,progress,token)=>
                {if(++cancelCalls==2){cancelBatch.Cancel();token.ThrowIfCancellationRequested();}return Task.FromResult(new List<DocumentImportDraft>{new DocumentImportDraft()});},drafts=>{stagedBeforeCancel+=drafts.Count;return drafts.Count;},null,cancelBatch.Token).GetAwaiter().GetResult();
            }
            catch(OperationCanceledException){stopped=true;}
            Require(stopped&&stagedBeforeCancel==1,"Cancellation lost a successful earlier review entry.");
            passes.Add("PASS batch cancellation preserves completed review entries without saving business records");
            using(var dialog=new DocumentImportDialog())
            {
                var stage=typeof(DocumentImportDialog).GetMethod("StageDrafts",BindingFlags.Instance|BindingFlags.NonPublic);
                var first=new DocumentImportDraft{FileHash=new string('a',64),DocumentNumber="QA-1"};
                int added=(int)stage.Invoke(dialog,new object[]{new List<DocumentImportDraft>{first}});
                int duplicate=(int)stage.Invoke(dialog,new object[]{new List<DocumentImportDraft>{new DocumentImportDraft{FileHash=first.FileHash}}});
                Require(added==1&&duplicate==0,"Repeated sources created duplicate review entries.");
                var queue=(BindingList<DocumentImportDraft>)typeof(DocumentImportDialog).GetField("_drafts",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(dialog);
                Require(queue.Count==1&&!queue[0].ReviewConfirmed,"Queued source was approved or lost.");
            }
            passes.Add("PASS discovered/uploaded sources share the review queue and hash deduplication; approval remains explicit");
            return string.Join(Environment.NewLine,passes)+Environment.NewLine+"Filesystem QA fixture retained: "+root;
        }
        private static LocalDocumentCandidate Candidate(string path)
        {
            var file=new FileInfo(path);return new LocalDocumentCandidate{FullPath=file.FullName,FileName=file.Name,SizeBytes=file.Length,ModifiedUtc=file.LastWriteTimeUtc,CanReview=true,Selected=true};
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
