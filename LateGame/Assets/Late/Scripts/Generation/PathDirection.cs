namespace Late.Generation
{
    /// <summary>
    /// Dirección en la que avanza el camino. El laberinto nunca retrocede, por eso no hay "Up".
    /// El orden de los valores no debe cambiar: se guarda como entero en los niveles horneados.
    /// </summary>
    public enum PathDirection
    {
        Left,
        Right,
        Down
    }
}
