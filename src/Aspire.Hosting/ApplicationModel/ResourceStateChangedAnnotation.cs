// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;
/// <summary>
/// 
/// </summary>
public class ResourceStateChangedAnnotation(string state) : IResourceAnnotation
{
    /// <summary>
    /// 
    /// </summary>
    public string State { get; private set; } = state;
    /// <summary>
    /// 
    /// </summary>
    public Action? StateChanged { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newState"></param>
    public void ChangeState(string newState)
    {
        if (State != newState)
        {
            State = newState;
            StateChanged?.Invoke();
        }
    }
}
