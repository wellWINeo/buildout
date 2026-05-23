using Microsoft.Extensions.Configuration;

namespace Buildout.Core.Configuration;

public sealed class HttpSectionRemapSource : IConfigurationSource
{
    private readonly IConfigurationRoot _parent;

    public HttpSectionRemapSource(IConfigurationRoot parent)
    {
        _parent = parent;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new HttpSectionRemapProvider(_parent);
    }

    private sealed class HttpSectionRemapProvider : ConfigurationProvider
    {
        private readonly IConfigurationRoot _parent;

        public HttpSectionRemapProvider(IConfigurationRoot parent)
        {
            _parent = parent;
        }

        public override void Load()
        {
            var timeout = _parent["Http:Timeout"];
            if (!string.IsNullOrEmpty(timeout))
            {
                Data["HttpTimeout"] = timeout;
            }

            var unsafeAllowInsecure = _parent["Http:UnsafeAllowInsecure"];
            if (!string.IsNullOrEmpty(unsafeAllowInsecure))
            {
                Data["UnsafeAllowInsecure"] = unsafeAllowInsecure;
            }
        }
    }
}