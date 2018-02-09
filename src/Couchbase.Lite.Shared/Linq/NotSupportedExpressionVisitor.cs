//
// NotSupportedExpressionVisitor.cs
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
#if CBL_LINQ
using System;
using System.Linq.Expressions;

namespace Couchbase.Lite.Internal.Linq
{
    internal abstract class NotSupportedExpressionVisitor : ExpressionVisitor
    {
        #region Variables

        protected bool FromSubclass;

        #endregion

        #region Overrides

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitBinary(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitBlock(node);
            }

            throw new NotSupportedException();
        }

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitCatchBlock(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitConditional(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitConstant(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitDebugInfo(DebugInfoExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitDebugInfo(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitDefault(DefaultExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitDefault(node);
            }

            throw new NotSupportedException();
        }

        protected override ElementInit VisitElementInit(ElementInit node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitElementInit(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitExtension(Expression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitExtension(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitGoto(GotoExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitGoto(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitIndex(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitInvocation(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitLabel(LabelExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitLabel(node);
            }

            throw new NotSupportedException();
        }

        protected override LabelTarget VisitLabelTarget(LabelTarget node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitLabelTarget(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitLambda(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitListInit(ListInitExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitListInit(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitLoop(LoopExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitLoop(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitMember(node);
            }

            throw new NotSupportedException();
        }

        protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitMemberAssignment(node);
            }

            throw new NotSupportedException();
        }

        protected override MemberBinding VisitMemberBinding(MemberBinding node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitMemberBinding(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitMemberInit(node);
            }

            throw new NotSupportedException();
        }

        protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitMemberListBinding(node);
            }

            throw new NotSupportedException();
        }

        protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitMemberMemberBinding(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitMethodCall(node);
            }

            throw new NotSupportedException($"Method {node.Method.Name} not supported");
        }

        protected override Expression VisitNew(NewExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitNew(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitNewArray(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitParameter(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitRuntimeVariables(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitSwitch(SwitchExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitSwitch(node);
            }

            throw new NotSupportedException();
        }

        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitSwitchCase(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitTry(TryExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitTry(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitTypeBinary(node);
            }

            throw new NotSupportedException();
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if(FromSubclass) {
                FromSubclass = false;
                return base.VisitUnary(node);
            }

            throw new NotSupportedException();
        }

        #endregion
    }
}
#endif