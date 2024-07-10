namespace GeneaGrab.Core.Models;

public class Progress
{
    public static readonly Progress Finished = new() { Value = 1, Done = true };
    public static readonly Progress Unknown = new() { Undetermined = true };
    private Progress() { }

    public static implicit operator Progress(int v) => new(v);
    public static implicit operator Progress(float v) => new(v);
    public static implicit operator Progress(decimal v) => new((float)v);
    public Progress(float value) => Value = value;

    public static implicit operator float(Progress p) => p.Value;
    public float Value { get; private set; }
    public bool Done { get; private set; }
    public bool Undetermined { get; private set; }
}
