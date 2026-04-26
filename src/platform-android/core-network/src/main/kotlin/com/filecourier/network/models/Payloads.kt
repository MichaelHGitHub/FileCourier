package com.filecourier.network.models

import com.google.gson.annotations.SerializedName

data class DiscoveryPayload(
    @SerializedName("SchemaVersion") val SchemaVersion: Int = 1,
    @SerializedName("DeviceId") val DeviceId: String,
    @SerializedName("DeviceName") val DeviceName: String,
    @SerializedName("MacAddress") val MacAddress: String = "",
    @SerializedName("OS") val OS: String = "Android",
    @SerializedName("TcpPort") val TcpPort: Int,
    @SerializedName("IsGoodbye") val IsGoodbye: Boolean = false
)

data class FileItem(
    @SerializedName("FileName") val FileName: String,
    @SerializedName("RelativePath") val RelativePath: String,
    @SerializedName("FileSize") val FileSize: Long,
    @SerializedName("ByteOffset") val ByteOffset: Long = 0,
    @SerializedName("Checksum") val Checksum: String? = null
)

data class TransferRequestHeader(
    @SerializedName("SchemaVersion") val SchemaVersion: Int = 2,
    @SerializedName("SenderId") val SenderId: String,
    @SerializedName("SenderName") val SenderName: String,
    @SerializedName("SenderMac") val SenderMac: String = "",
    @SerializedName("IsEncrypted") val IsEncrypted: Boolean = false,
    @SerializedName("TextPayload") val TextPayload: String? = null,
    @SerializedName("Files") val Files: List<FileItem>? = null
)

data class TransferResponseHeader(
    @SerializedName("SchemaVersion") val SchemaVersion: Int = 1,
    @SerializedName("Status") val Status: Int, // 0 = Accepted, 1 = Rejected
    @SerializedName("Reason") val Reason: String? = null
)
