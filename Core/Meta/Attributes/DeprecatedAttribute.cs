﻿namespace Core.Meta.Attributes
{
    /// <summary>
    /// An attribute that deprecates fields of messages so they are skipped over.
    /// </summary>
    public sealed class DeprecatedAttribute : BaseAttribute
    {

        public DeprecatedAttribute(string value)
        {
            
            Name = "deprecated";
            Value = value;
        }

        /// <summary>
        /// Validates whether a reason was provided in the deprecation attribute.
        /// </summary>
        /// <returns></returns>
        public override bool TryValidate(out string message)
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                message = "deprecation attributes must contain a reason.";
                return false;
            }
            message = string.Empty;
            return true;

        }
    }
}