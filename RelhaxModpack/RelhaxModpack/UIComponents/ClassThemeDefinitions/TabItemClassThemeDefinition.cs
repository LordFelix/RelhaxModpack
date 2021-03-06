﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RelhaxModpack.UIComponents.ClassThemeDefinitions
{
    /// <summary>
    /// The UI class theme definition for TabItem class
    /// </summary>
    public class TabItemClassThemeDefinition : IClassThemeDefinition
    {
        /// <summary>
        /// The Type object that corresponds to this UI class
        /// </summary>
        public Type ClassType { get; } = typeof(TabItem);

        //background
        /// <summary>
        /// Determines if background color is allowed to change
        /// </summary>
        public bool BackgroundAllowed { get; } = true;

        /// <summary>
        /// The name of the component for WPF databinding. An empty string indicates the component is not bound.
        /// </summary>
        public string BackgroundBoundName { get; } = string.Empty;

        /// <summary>
        /// Determines if the color is applied locally as to not interfere with dependency source ordering
        /// </summary>
        /// <remarks>See https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-value-precedence#setcurrentvalue </remarks>
        public bool BackgroundAppliedLocal { get; } = false;

        /// <summary>
        /// The reference to the Background DependencyProperty (if exists) of this class type for setting the value locally
        /// </summary>
        public DependencyProperty BackgroundDependencyProperty { get; } = TabItem.BackgroundProperty;

        //foreground
        /// <summary>
        /// Determines if Foreground color is allowed to change
        /// </summary>
        public bool ForegroundAllowed { get; } = true;

        /// <summary>
        /// The name of the component for WPF databinding. An empty string indicates the component is not bound.
        /// </summary>
        public string ForegroundBoundName { get; } = string.Empty;

        /// <summary>
        /// Determines if the color is applied locally as to not interfere with dependency source ordering
        /// </summary>
        /// <remarks>See https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-value-precedence#setcurrentvalue </remarks>
        public bool ForegroundAppliedLocal { get; } = false;

        /// <summary>
        /// The reference to the Foreground DependencyProperty (if exists) of this class type for setting the value locally
        /// </summary>
        public DependencyProperty ForegroundDependencyProperty { get; } = TabItem.ForegroundProperty;

        //highlight
        /// <summary>
        /// Determines if Highlight color is allowed to change. Highlight can only be changed by WPF data-bind
        /// </summary>
        public bool HighlightAllowed { get; } = true;

        /// <summary>
        /// The name of the component for WPF databinding. An empty string indicates the component is not bound.
        /// </summary>
        public string HighlightBoundName { get; } = nameof(BoundUISettings.TabItemHighlightBrush);

        //select
        /// <summary>
        /// Determines if Select (mouse button down color) color is allowed to change. Select can only be changed by WPF data-bind
        /// </summary>
        public bool SelectAllowed { get; } = true;

        /// <summary>
        /// The name of the component for WPF databinding. An empty string indicates the component is not bound.
        /// </summary>
        public string SelectBoundName { get; } = nameof(BoundUISettings.TabItemSelectedBrush);
    }
}
