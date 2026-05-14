using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Authentication;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Authoring.Properties;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
using Buildout.Core.Search;
using Buildout.Core.Search.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Buildout.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBuildinClient(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new BuildinClientOptions();
        configuration.GetSection("Buildin").Bind(options);

        var validator = new BuildinClientOptionsValidator();
        var validationResult = validator.Validate(null, options);
        if (validationResult.Failed)
            throw new InvalidOperationException(validationResult.FailureMessage);

        services.AddSingleton(options);
        services.AddSingleton<IValidateOptions<BuildinClientOptions>>(_ => validator);
        services.AddSingleton<IAuthenticationProvider>(sp =>
        {
            var opts = sp.GetRequiredService<BuildinClientOptions>();
            return new BotTokenAuthenticationProvider(opts.BotToken);
        });
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<BuildinClientOptions>();
            return new HttpClient
            {
                BaseAddress = opts.BaseUrl,
                Timeout = opts.HttpTimeout
            };
        });

        return services;
    }

    public static IServiceCollection AddBuildoutCore(this IServiceCollection services)
    {
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new ParagraphConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new Heading1Converter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new Heading2Converter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new Heading3Converter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new BulletedListItemConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new NumberedListItemConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new ToDoConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new CodeConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new QuoteConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static _ => new DividerConverter());
        services.AddSingleton<IBlockToMarkdownConverter>(static sp =>
            new ChildDatabaseConverter(sp.GetRequiredService<IDatabaseViewRenderer>()));

        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new PageMentionConverter());
        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new DatabaseMentionConverter());
        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new UserMentionConverter());
        services.AddSingleton<IMentionToMarkdownConverter>(static _ => new DateMentionConverter());

        services.AddSingleton<BlockToMarkdownRegistry>();
        services.AddSingleton<MentionToMarkdownRegistry>();

        services.AddSingleton<IInlineRenderer, InlineRenderer>();
        services.AddSingleton<IPageMarkdownRenderer, PageMarkdownRenderer>();

        services.AddSingleton<ITitleRenderer, TitleRenderer>();
        services.AddSingleton<AncestorScopeFilter>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<ISearchResultFormatter, SearchResultFormatter>();

        services.AddSingleton<CellBudget>(static _ => new CellBudget(24, "…"));
        services.AddSingleton<IPropertyValueFormatter, PropertyValueFormatter>();
        services.AddSingleton<IDatabaseViewStyle, TableViewStyle>();
        services.AddSingleton<IDatabaseViewStyle, BoardViewStyle>();
        services.AddSingleton<IDatabaseViewStyle, GalleryViewStyle>();
        services.AddSingleton<IDatabaseViewStyle, ListViewStyle>();
        services.AddSingleton<IDatabaseViewStyle, CalendarViewStyle>();
        services.AddSingleton<IDatabaseViewStyle, TimelineViewStyle>();
        services.AddSingleton<IDatabaseViewRenderer, DatabaseViewRenderer>();
        services.AddSingleton<IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>>(sp =>
        {
            var styles = sp.GetServices<IDatabaseViewStyle>();
            return styles.ToDictionary(s => s.Key);
        });

        services.AddSingleton<IMarkdownToBlocksParser, MarkdownToBlocksParser>();
        services.AddSingleton<IDatabasePropertyValueParser, DatabasePropertyValueParser>();
        services.AddSingleton<ParentKindProbe>();
        services.AddSingleton<IPageCreator, PageCreator>();

        return services;
    }
}
