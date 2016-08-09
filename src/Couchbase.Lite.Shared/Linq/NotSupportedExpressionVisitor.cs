//
// NotSupportedQueryProvider.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Linq.Expressions;

namespace Couchbase.Lite.Linq
{
    internal abstract class NotSupportedExpressionVisitor : ExpressionVisitor
    {
        protected bool _fromSubclass;

        protected override Expression VisitConditional (ConditionalExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitConditional (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitBinary (BinaryExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitBinary (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitBlock (BlockExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitBlock (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitTry (TryExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitTry (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitNew (NewExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitNew (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitGoto (GotoExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitGoto (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitLoop (LoopExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitLoop (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitExtension (Expression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitExtension (node);
            }

            throw new NotSupportedException ();
        }

        protected override SwitchCase VisitSwitchCase (SwitchCase node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitSwitchCase (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitUnary (UnaryExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitUnary (node);
            }

            throw new NotSupportedException ();
        }

        protected override CatchBlock VisitCatchBlock (CatchBlock node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitCatchBlock (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitLabel (LabelExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitLabel (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitIndex (IndexExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitIndex (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitDefault (DefaultExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitDefault (node);
            }

            throw new NotSupportedException ();
        }

        protected override ElementInit VisitElementInit (ElementInit node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitElementInit (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitMember (MemberExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitMember (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitSwitch (SwitchExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitSwitch (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitLambda<T> (Expression<T> node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitLambda (node);
            }

            throw new NotSupportedException ();
        }

        protected override LabelTarget VisitLabelTarget (LabelTarget node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitLabelTarget (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitConstant (ConstantExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitConstant (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitDynamic (DynamicExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitDynamic (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitListInit (ListInitExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitListInit (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitNewArray (NewArrayExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitNewArray (node);
            }

            throw new NotSupportedException ();
        }

        protected override MemberBinding VisitMemberBinding (MemberBinding node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitMemberBinding (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitDebugInfo (DebugInfoExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitDebugInfo (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitParameter (ParameterExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitParameter (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitInvocation (InvocationExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitInvocation (node);
            }

            throw new NotSupportedException ();
        }

        protected override MemberAssignment VisitMemberAssignment (MemberAssignment node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitMemberAssignment (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitMemberInit (MemberInitExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitMemberInit (node);
            }

            throw new NotSupportedException ();
        }

        protected override MemberListBinding VisitMemberListBinding (MemberListBinding node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitMemberListBinding (node);
            }

            throw new NotSupportedException ();
        }

        protected override MemberMemberBinding VisitMemberMemberBinding (MemberMemberBinding node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitMemberMemberBinding (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitMethodCall (MethodCallExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitMethodCall (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitRuntimeVariables (RuntimeVariablesExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitRuntimeVariables (node);
            }

            throw new NotSupportedException ();
        }

        protected override Expression VisitTypeBinary (TypeBinaryExpression node)
        {
            if (_fromSubclass) {
                _fromSubclass = false;
                return base.VisitTypeBinary (node);
            }

            throw new NotSupportedException ();
        }
    }
}

