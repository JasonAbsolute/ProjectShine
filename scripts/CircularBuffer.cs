using System;
using System.Collections.Generic;

public class CircularBuffer<T>
{
     private readonly List<T> list;
    private readonly int maxSize;

    public CircularBuffer(int size)
    {
        maxSize = size;
        list = new List<T>(size);
    }

    public void Add(T item)
    {
        if (list.Count >= maxSize)
        {
            // Remove the oldest item (at index 0)
            list.RemoveAt(0);
        }
        list.Add(item);
    }

    public bool Contains(T item)
    {
        return list.Contains(item);
    }

    public void PrintList()
    {
        Console.WriteLine("List contents:");
        foreach (var item in list)
        {
            Console.WriteLine(item);
        }
    }

    public int Count => list.Count;
}