# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[cmdletbinding()]
param(
    [string]$ContainerName,
    [string]$Filename,
    [string]$BlobName,
    [string]$AccountName = "dockertoolsbuilddrop",
    [string]$AccountKey
)

$blobContext = New-AzureStorageContext -StorageAccountName $AccountName -StorageAccountKey $AccountKey
Set-AzureStorageBlobContent -File $Filename -Container $ContainerName -Blob $BlobName -Context $blobContext -Force
write-host "'$Filename' uploaded to '$ContainerName'!"