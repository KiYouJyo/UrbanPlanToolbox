namespace UrbanPlanToolbox.Services;

public static class ReferenceLibraryText
{
    private static readonly IReadOnlyDictionary<string, string[]> Values = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["BackDesign"] = ["← 设计工具", "← デザインツール", "← Design tools"],
        ["BackResearch"] = ["← 科研工具", "← 研究ツール", "← Research tools"],
        ["CurrentSource"] = ["当前数据源", "現在のデータソース", "Current data source"],
        ["NoDataPack"] = ["尚未安装数据包", "データパックが未インストールです", "No data pack installed"],
        ["NoDataPackHint"] = ["从 UrbanPlanToolbox_Data 检查官方数据更新，或导入本地 .uptdata。", "UrbanPlanToolbox_Data から公式データを確認するか、ローカルの .uptdata を読み込んでください。", "Check UrbanPlanToolbox_Data for official data or import a local .uptdata pack."],
        ["CheckUpdate"] = ["检查更新", "更新を確認", "Check updates"],
        ["CheckDataUpdate"] = ["↻ 检查数据更新", "↻ データ更新を確認", "↻ Check data updates"],
        ["ManageSource"] = ["管理数据源", "データソースを管理", "Manage source"],
        ["ImportPack"] = ["导入 .uptdata", ".uptdata を読み込む", "Import .uptdata"],
        ["Rollback"] = ["回退上一版本", "前のバージョンへ戻す", "Roll back"],
        ["Close"] = ["关闭", "閉じる", "Close"],
        ["Latest"] = ["最新", "最新", "Latest"],
        ["CloudVersion"] = ["云端版本：{0} · {1}", "クラウド版：{0} · {1}", "Cloud version: {0} · {1}"],
        ["CloudUnavailable"] = ["云端版本：暂不可用", "クラウド版：現在利用できません", "Cloud version: unavailable"],
        ["PackMeta"] = ["{0} · {1} 条 · Schema {2}", "{0} · {1} 件 · Schema {2}", "{0} · {1} entries · Schema {2}"],
        ["UpdateAvailable"] = ["发现数据更新 {0}", "データ更新 {0} が見つかりました", "Data update {0} is available"],
        ["DownloadInstall"] = ["下载并安装", "ダウンロードしてインストール", "Download and install"],
        ["UpdateInstalled"] = ["数据包已更新到 {0}。", "データパックを {0} に更新しました。", "Data pack updated to {0}."],
        ["AlreadyLatest"] = ["当前数据包已是最新版本。", "現在のデータパックは最新です。", "The current data pack is up to date."],
        ["CatalogUnavailable"] = ["官方数据目录暂不可用。可继续使用已安装数据或导入本地 .uptdata。", "公式データカタログを利用できません。インストール済みデータまたはローカル .uptdata を利用できます。", "The official data catalog is unavailable. Installed data and local .uptdata packs remain usable."],
        ["ImportSucceeded"] = ["数据包已验证并启用：{0}", "データパックを検証して有効化しました：{0}", "Data pack validated and activated: {0}"],
        ["RollbackSucceeded"] = ["已回退到上一版数据包。", "前のデータパックに戻しました。", "Rolled back to the previous data pack."],
        ["NoRollback"] = ["没有可回退的数据包版本。", "戻せるデータパックがありません。", "No previous data pack is available."],
        ["PackFailed"] = ["数据包操作失败：{0}", "データパック操作に失敗しました：{0}", "Data pack operation failed: {0}"],
        ["SourceManagerTitle"] = ["数据源管理", "データソース管理", "Data source manager"],
        ["SourceManagerBody"] = ["当前：{0}\n来源：{1}\n数据包会在切换前完成格式、Schema、路径与内容校验。旧版本保留用于回退。", "現在：{0}\nソース：{1}\n切替前に形式、Schema、パス、内容を検証し、旧版はロールバック用に保持します。", "Current: {0}\nSource: {1}\nFormat, schema, paths and data are validated before activation. Previous versions are retained for rollback."],
        ["SourceOfficial"] = ["UrbanPlanToolbox_Data 官方数据", "UrbanPlanToolbox_Data 公式データ", "UrbanPlanToolbox_Data official data"],
        ["SourceLocal"] = ["本地导入", "ローカル読み込み", "Local import"],
        ["SourceRollback"] = ["历史版本", "履歴バージョン", "Previous version"],
        ["SourceInstalled"] = ["已安装数据包", "インストール済みデータ", "Installed data pack"],
        ["ResetFilters"] = ["清除筛选", "フィルターを解除", "Clear filters"],
        ["NoResults"] = ["没有符合当前条件的条目。", "条件に一致する項目がありません。", "No entries match the current filters."],
        ["OpenSource"] = ["打开来源", "出典を開く", "Open source"],
        ["Copy"] = ["复制引用", "引用をコピー", "Copy citation"],
        ["Copied"] = ["已复制到剪贴板。", "クリップボードにコピーしました。", "Copied to clipboard."],
        ["RegSearch"] = ["搜索法规名称、编号、发布机关或关键词", "法規名・番号・発行機関・キーワードを検索", "Search title, identifier, authority or keyword"],
        ["AllRegions"] = ["全部地区", "すべての地域", "All regions"],
        ["AllTopics"] = ["全部主题", "すべてのテーマ", "All topics"],
        ["AllStatus"] = ["全部状态", "すべての状態", "All statuses"],
        ["RegCount"] = ["共 {0} 条 · 显示 {1} 条", "全 {0} 件 · {1} 件表示", "{0} total · {1} shown"],
        ["RegEntries"] = ["法规条目", "法規項目", "Regulations"],
        ["Summary"] = ["摘要", "概要", "Summary"],
        ["ApplicableTags"] = ["适用标签", "適用タグ", "Applicable tags"],
        ["Sources"] = ["来源", "出典", "Sources"],
        ["OpenOfficial"] = ["打开官方来源", "公式情報を開く", "Open official source"],
        ["DownloadSource"] = ["下载来源文件", "出典ファイルを開く", "Open source file"],
        ["OpenBrowser"] = ["在浏览器打开", "ブラウザーで開く", "Open in browser"],
        ["TermSearch"] = ["搜索中文、日本語、English 或定义关键词", "中国語・日本語・English・定義を検索", "Search Chinese, Japanese, English or definitions"],
        ["AllJurisdictions"] = ["全部法域", "すべての法域", "All jurisdictions"],
        ["AllCategories"] = ["全部分类", "すべての分類", "All categories"],
        ["TermCount"] = ["{0} 个术语 · 当前筛选 {1} 个", "{0} 用語 · 現在 {1} 件", "{0} terms · {1} filtered"],
        ["Terms"] = ["术语", "用語", "Terms"],
        ["Definition"] = ["定义", "定義", "Definition"],
        ["Comparison"] = ["对应与差异", "対応と相違", "Equivalence & differences"],
        ["RelatedTerms"] = ["相关术语", "関連用語", "Related terms"],
        ["CopyTerm"] = ["复制术语", "用語をコピー", "Copy term"],
        ["ConceptSearch"] = ["搜索理念名称、定义、项目类型、标签、案例或来源", "名称・定義・プロジェクト種別・タグ・事例・出典を検索", "Search concepts, definitions, project types, tags, cases or sources"],
        ["AllProjectTypes"] = ["全部项目类型", "すべてのプロジェクト種別", "All project types"],
        ["AllTags"] = ["全部标签", "すべてのタグ", "All tags"],
        ["Recent"] = ["最近更新", "最近の更新", "Recently reviewed"],
        ["NameSort"] = ["名称", "名称", "Name"],
        ["ConceptCount"] = ["{0} 条理念 · 显示 {1} 条", "{0} 件 · {1} 件表示", "{0} concepts · {1} shown"],
        ["ConceptEntries"] = ["理念条目", "コンセプト", "Concepts"],
        ["ProjectTypes"] = ["适用项目类型", "適用プロジェクト種別", "Project types"],
        ["Tags"] = ["标签", "タグ", "Tags"],
        ["CaseNote"] = ["案例说明", "事例メモ", "Case note"],
        ["ViewSource"] = ["查看来源", "出典を見る", "View source"],
        ["Verified"] = ["已核验", "検証済み", "Verified"],
        ["Reviewed"] = ["已审核", "レビュー済み", "Reviewed"],
        ["Seed"] = ["初始资料", "初期資料", "Seed"],
        ["Current"] = ["现行", "現行", "Current"],
        ["Draft"] = ["草案", "案", "Draft"],
        ["Archived"] = ["历史", "旧版", "Archived"]
    };

    public static string Get(string key, params object[] args)
    {
        if (!Values.TryGetValue(key, out var translations)) return key;
        var language = LocalizationService.Default.CurrentLanguage;
        var index = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? 1 : language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        return args.Length == 0 ? translations[index] : string.Format(translations[index], args);
    }
}
