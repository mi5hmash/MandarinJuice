namespace MandarinJuiceCore.Models.DSSS.Mandarin;

/// <summary>
/// Represents a queue of byte values that tracks the total length and provides shifted operations based on a specified shift value.
/// </summary>
/// <param name="shift">The number of bits to shift when performing shifted operations. Must be between 0 and 7, inclusive.</param>
public class MandarinSlicesQueue(byte shift) : Queue<byte>
{
    /// <summary>
    /// Gets the shift value used in the queue operations.
    /// </summary>
    public byte Shift { get; } = shift;

    /// <summary>
    /// Gets the total length of all objects in the queue.
    /// </summary>
    public int TotalLength { get; private set; }

    /// <summary>
    /// Gets the total length of all objects in the queue after applying the shift value.
    /// </summary>
    public int TotalLengthShifted => TotalLength << Shift;

    /// <summary>
    /// Adds the object to the end of the <see cref="MandarinSlicesQueue"/>.
    /// </summary>
    /// <param name="item">The object to add to the queue.</param>
    public new void Enqueue(byte item)
    {
        base.Enqueue(item);
        TotalLength += item;
    }

    /// <summary>
    /// Adds the shifted object to the end of the <see cref="MandarinSlicesQueue"/>.
    /// </summary>
    /// <param name="item">The object to add to the queue.</param>
    public void EnqueueShifted(int item)
    {
        var localItem = (byte)(item >> Shift);
        Enqueue(localItem);
    }

    /// <summary>
    /// Removes and returns the item at the beginning of the queue, updating the total length accordingly.
    /// </summary>
    /// <returns>The integer value removed from the beginning of the queue.</returns>
    public new int Dequeue()
    {
        var item = base.Dequeue();
        TotalLength -= item;
        return item;
    }

    /// <summary>
    /// Removes the next item from the queue and returns its value left-shifted by the configured number of bits.
    /// </summary>
    /// <returns>The value of the dequeued item, left-shifted by the value of <see cref="Shift"/>.</returns>
    public int DequeueShifted()
    {
        var item = Dequeue();
        return item << Shift;
    }

    /// <summary>
    /// Returns the value obtained by left-shifting the peeked element from queue by the current shift amount.
    /// </summary>
    /// <returns>The value of the peeked item, left-shifted by the value of <see cref="Shift"/>.</returns>
    public int PeekShifted()
    {
        var item = Peek();
        return item << Shift;
    }
}