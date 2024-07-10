namespace GeneaGrab.Core.Models;

/// <summary>The type of data contained in the registry</summary>
public enum RegistryType
{
    /// <summary>Unable to determine</summary>
    Unknown = -2,
    /// <summary>Uncategorized</summary>
    Other = -1,


    #region Civil status

    /// <summary>Birth certificates</summary>
    Birth,
    /// <summary>Table of birth certificates</summary>
    BirthTable,
    /// <summary>Baptismal records</summary>
    Baptism,
    /// <summary>Table of baptismal records</summary>
    BaptismTable,
    /// <summary>Communions records</summary>
    Communion,
    /// <summary>Confirmation records</summary>
    Confirmation,
    /// <summary>Banns of marriage</summary>
    Banns,
    /// <summary>Marriage certificates</summary>
    Marriage,
    /// <summary>Table of marriage certificates</summary>
    MarriageTable,
    /// <summary>Process of terminating a marriage or marital union</summary>
    Divorce,
    /// <summary>Death certificates</summary>
    Death,
    /// <summary>Table of death certificates</summary>
    DeathTable,
    /// <summary>Burial records</summary>
    Burial,
    /// <summary>Table of burial records</summary>
    BurialTable,

    #endregion


    #region Census

    /// <summary>Census of the population</summary>
    Census,
    /// <summary>Register recording the biographical and religious data of parishioners</summary>
    LiberStatutAnimarum,

    #endregion


    #region Notarial

    /// <summary>Uncategorized notarial deeds</summary>
    Notarial,
    /// <summary>Methodical list where the subjects are arranged in an order that makes it easy to find them</summary>
    Catalogue,
    /// <summary>The minutes, or official record, of a negotiation or transaction; especially a document drawn up officially which forms the legal basis for subsequent agreements based on it</summary>
    Protocol,
    /// <summary>Original of an authentic act kept by the authority which holds it and which cannot separate from it (notarial deed in the case of a notary, court decision in the case of a jurisdiction...)</summary>
    Minutes,
    /// <summary>A copy, by a notary, of an obligation, a contract, etc., or, by a court clerk, of a judgment, of a ruling, which is delivered in enforceable form and which was usually written in larger characters than the minute. Sometimes called an enforceable copy</summary>
    Engrossments,

    #endregion

    #region Publications

    /// <summary>Work that is not intended to be published on a regular basis</summary>
    Book,
    /// <summary>A publication, usually published daily or weekly, containing news and other articles</summary>
    Newspaper,
    /// <summary>A publication issued regularly, but less frequently than daily</summary>
    Periodical,

    #endregion

    #region Cadastre

    /// <summary>Indication of the layout of the cadastral sections</summary>
    CadastralAssemblyTable,
    /// <summary>Overview of cadastres in a registration division</summary>
    CadastralMap,
    /// <summary>Indicative table of land properties, their areas and their income</summary>
    CadastralSectionStates,
    /// <summary>Register showing the increases and decreases in areas and income recorded on the cadastral matrices</summary>
    CadastralMatrix,

    #endregion


    #region Military

    /// <summary>Military numbers</summary>
    Military

    #endregion
}
