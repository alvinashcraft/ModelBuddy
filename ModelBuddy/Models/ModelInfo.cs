namespace ModelBuddy.Models;

/// <summary>
/// Represents information about a Foundry Local AI model.
/// </summary>
public class LocalModel
{
    /// <summary>
    /// The unique identifier of the model.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// A human-friendly alias for the model (e.g., "phi-3.5-mini").
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the model.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The provider/publisher of the model (e.g., "Microsoft").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The model task type (e.g., "chat-completion", "text-generation").
    /// </summary>
    public string Task { get; set; } = string.Empty;

    /// <summary>
    /// The target device type (e.g., "CPU", "GPU", "NPU").
    /// </summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>
    /// The download status of the model.
    /// </summary>
    public ModelStatus Status { get; set; } = ModelStatus.Available;

    /// <summary>
    /// The size of the model in bytes.
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// The estimated RAM required to run the model in bytes.
    /// </summary>
    public long EstimatedRamInBytes { get; set; }

    /// <summary>
    /// The maximum token context length supported by the model.
    /// </summary>
    public int MaxTokens { get; set; }

    /// <summary>
    /// Indicates whether the model can run on the current system based on available RAM.
    /// </summary>
    public bool CanRun { get; set; }

    /// <summary>
    /// Gets the high-level category of the model derived from <see cref="Task"/>.
    /// </summary>
    public ModelCategory Category => ClassifyTask(Task);

    /// <summary>
    /// Gets a short human-friendly label for <see cref="Category"/> (e.g., "Chat", "Embeddings").
    /// </summary>
    public string CategoryLabel => Category switch
    {
        ModelCategory.ChatCompletion => "Chat",
        ModelCategory.Vision => "Vision",
        ModelCategory.Embeddings => "Embeddings",
        ModelCategory.Transcription => "Transcription",
        _ => string.IsNullOrWhiteSpace(Task) ? "Other" : Task,
    };

    /// <summary>
    /// Indicates whether this model can be used for chat (chat-completion or vision/multimodal).
    /// </summary>
    public bool IsChatCapable => Category is ModelCategory.ChatCompletion or ModelCategory.Vision;

    /// <summary>
    /// Gets the formatted size string (e.g., "2.5 GB").
    /// </summary>
    public string FormattedSize => FormatBytes(SizeInBytes);

    /// <summary>
    /// Gets the formatted estimated RAM string (e.g., "3.0 GB").
    /// </summary>
    public string FormattedEstimatedRam => FormatBytes(EstimatedRamInBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "N/A";

        const double gb = 1024 * 1024 * 1024;
        const double mb = 1024 * 1024;

        if (bytes >= gb)
            return $"{bytes / gb:F1} GB";
        if (bytes >= mb)
            return $"{bytes / mb:F1} MB";

        return $"{bytes / 1024.0:F1} KB";
    }

    private static ModelCategory ClassifyTask(string? task)
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            return ModelCategory.Other;
        }

        var t = task.ToLowerInvariant();

        if (t.Contains("embed"))
        {
            return ModelCategory.Embeddings;
        }

        if (t.Contains("speech") || t.Contains("transcription") || t.Contains("asr") || t.Contains("audio"))
        {
            return ModelCategory.Transcription;
        }

        if (t.Contains("vision") || t.Contains("image") || t.Contains("multimodal") || t.Contains("vqa"))
        {
            return ModelCategory.Vision;
        }

        if (t.Contains("chat") || t.Contains("text-generation") || t.Contains("text2text"))
        {
            return ModelCategory.ChatCompletion;
        }

        return ModelCategory.Other;
    }
}

/// <summary>
/// High-level categorization of a Foundry Local model based on its task.
/// </summary>
public enum ModelCategory
{
    /// <summary>Chat-completion / text-generation models.</summary>
    ChatCompletion,

    /// <summary>Multimodal vision-language models (e.g., Qwen 3.5 VL).</summary>
    Vision,

    /// <summary>Text embedding models (e.g., qwen3-0.6b-embedding).</summary>
    Embeddings,

    /// <summary>Speech-to-text / live transcription models (e.g., Nemotron ASR).</summary>
    Transcription,

    /// <summary>Any other or unrecognized task.</summary>
    Other,
}

/// <summary>
/// Represents the download/availability status of a model.
/// </summary>
public enum ModelStatus
{
    /// <summary>
    /// The model is available for download but not yet downloaded.
    /// </summary>
    Available,

    /// <summary>
    /// The model is currently being downloaded.
    /// </summary>
    Downloading,

    /// <summary>
    /// The model is downloaded and ready to use.
    /// </summary>
    Downloaded,

    /// <summary>
    /// The model is currently loaded and running.
    /// </summary>
    Loaded
}
