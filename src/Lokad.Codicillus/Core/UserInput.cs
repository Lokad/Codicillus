namespace Lokad.Codicillus.Core;

public abstract record UserInput;

public sealed record UserInputText(string Text) : UserInput;

public sealed record UserInputImage(string ImageUrl) : UserInput;
