param([string]$Server='.\SQLEXPRESS',[string]$SourceDatabase='HVAC_PRO')
$ErrorActionPreference='Stop'
# Read only SQL metadata from source. No customer rows are copied. All writes target a new, uniquely named QA database.
$name='ServoERP_DocumentImportQA_'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
function Open-Connection([string]$catalog) {
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']=$Server; $builder['Initial Catalog']=$catalog; $builder['Integrated Security']=$true; $builder['TrustServerCertificate']=$true
    $c=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString; $c.Open(); return $c
}
function Execute($c,[string]$sql) { $cmd=$c.CreateCommand(); $cmd.CommandText=$sql; try { $null=$cmd.ExecuteNonQuery() } finally { $cmd.Dispose() } }
$tables=@('B2BClients','ClientSites','Vendors','StockItems','Jobs','Quotations','QuotationLineItems','Invoices','InvoiceLineItems','PurchaseOrders','PurchaseLineItems','ClientDocuments')
$source=Open-Connection $SourceDatabase
$ddl=@()
try {
    foreach($table in $tables) {
        $cmd=$source.CreateCommand()
        $cmd.CommandText=@'
SELECT c.name,ty.name TypeName,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,dc.definition DefaultDefinition,
 CONVERT(nvarchar(max),cc.definition) ComputedDefinition
FROM sys.columns c JOIN sys.types ty ON c.user_type_id=ty.user_type_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id=dc.object_id
LEFT JOIN sys.computed_columns cc ON c.object_id=cc.object_id AND c.column_id=cc.column_id
WHERE c.object_id=OBJECT_ID(@Table) ORDER BY c.column_id
'@
        $null=$cmd.Parameters.AddWithValue('@Table','dbo.'+$table)
        $adapter=New-Object System.Data.SqlClient.SqlDataAdapter $cmd; $data=New-Object System.Data.DataTable; $null=$adapter.Fill($data); $adapter.Dispose(); $cmd.Dispose()
        if($data.Rows.Count -eq 0) {throw "Source schema missing $table"}
        $columns=@()
        foreach($row in $data.Rows) {
            $column='['+([string]$row.name).Replace(']',']]')+'] '
            if($row.ComputedDefinition -isnot [DBNull]) { $column+='AS '+$row.ComputedDefinition; $columns+=$column; continue }
            $type=[string]$row.TypeName
            $column+=$type
            if($type -in @('nvarchar','nchar','varchar','char','varbinary','binary')) {
                $length=[int]$row.max_length
                if($length -eq -1){$column+='(max)'} else {if($type.StartsWith('n')){$length=$length/2};$column+='('+$length+')'}
            } elseif($type -in @('decimal','numeric')) {$column+='('+$row.precision+','+$row.scale+')'}
            if($row.is_identity){$column+=' IDENTITY(1,1) PRIMARY KEY'}
            $column+= $(if($row.is_nullable){' NULL'}else{' NOT NULL'})
            if($row.DefaultDefinition -isnot [DBNull]){$column+=' DEFAULT '+$row.DefaultDefinition}
            $columns+=$column
        }
        $ddl+='CREATE TABLE dbo.['+$table+']('+($columns -join ',')+');'
    }
} finally {$source.Dispose()}
$master=Open-Connection 'master'
try {Execute $master ('CREATE DATABASE ['+$name+']')} finally {$master.Dispose()}
$target=Open-Connection $name
try {
    foreach($sql in $ddl){Execute $target $sql}
    Execute $target 'CREATE VIEW dbo.ActiveInvoices AS SELECT * FROM dbo.Invoices WHERE IsDeleted=0;'
    Execute $target 'CREATE VIEW dbo.FinancialInvoices AS SELECT * FROM dbo.Invoices WHERE IsDeleted=0 OR IsPosted=1;'
    Execute $target 'CREATE VIEW dbo.ActiveJobs AS SELECT * FROM dbo.Jobs WHERE IsDeleted=0;'
    Execute $target @'
ALTER TABLE dbo.InvoiceLineItems ADD FOREIGN KEY(InvoiceID) REFERENCES dbo.Invoices(InvoiceID);
ALTER TABLE dbo.QuotationLineItems ADD FOREIGN KEY(TenderBidId) REFERENCES dbo.Quotations(BidID);
ALTER TABLE dbo.PurchaseLineItems ADD FOREIGN KEY(POID) REFERENCES dbo.PurchaseOrders(POID);
ALTER TABLE dbo.Invoices ADD FOREIGN KEY(ClientID) REFERENCES dbo.B2BClients(ClientID);
ALTER TABLE dbo.PurchaseOrders ADD FOREIGN KEY(VendorID) REFERENCES dbo.Vendors(VendorID);
CREATE UNIQUE INDEX UQ_ImportTestInvoiceNumber ON dbo.Invoices(InvoiceNumber) WHERE InvoiceNumber IS NOT NULL;
INSERT dbo.B2BClients(CompanyName) VALUES(N'Import QA Customer');
INSERT dbo.Vendors(VendorName) VALUES(N'Import QA Supplier');
INSERT dbo.StockItems(ItemName,CurrentStock,ReservedStock) VALUES(N'Import QA Product',25,0);
'@
} finally {$target.Dispose()}
$name
