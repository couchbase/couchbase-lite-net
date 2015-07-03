ALL INFORMATION IN THIS FILE IS SUBJECT TO CHANGE

This repo follows the following commit pattern:

`master` should strive to always be buildable and as stable as possible.  This branch will never be rebased, or have any other funny business going on that would affect a local copy's ability to retrieve further commits.

When a release is nearing, a release branch will be created in the form `release/<Major>.<Minor>.<Revision>`.  In general, a *Major* number increment will bring along some changes to public API.  A *Minor* change will focus on bringing new either new functionality or optimizations for existing functionality.  A *Revision* change will focus on bug fixing.  For the time being, this branch is **not** merged back into master at the end of its lifecycle, but relevant changes are cherrypicked back onto master.  This can, and probably will, change in the future.  The release of a version of Couchbase Lite will be the final commit on the release branch.

When tackling an issue that is non-trivial, an issue branch is made in the form `issue/<github-issue-number>`.  If this branch gets out of date, it will be **rebased** on master.  That means that at any time, an issue branch may become invalid for someone who has pulled it, and they will need to delete and re-pull the branch.  Once the issue is finished, a pull request will be issued and it will be merged into master without fast forward (i.e. making a merge commit).  The issue branch will then be deleted.  Efforts will be made to include keywords that will both close the issue automatically on GitHub and link the pull request with the issue on waffle.io.  

When developing a new feature, a feature branch is made in the form of `feature/<name-of-feature>`.  The name of the feature is arbitrary.  If this branch gets out of date then, like an issue branch, it will be rebased on master and therefore it may become invalid.  Once finished, a pull request is issued and it will be merged into master and the feature branch will be deleted.
